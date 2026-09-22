import { Component, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ConversationService,
  StreamEvent,
} from './services/conversation.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnDestroy {
  conversationId = signal('');
  runId = signal('');

  message = '';
  response = signal('');
  userMessage = signal('');

  lastSequence = signal(0);

  status = signal<
    | 'Disconnected'
    | 'Connecting'
    | 'Connected'
    | 'Reconnecting'
    | 'Completed'
    | 'Failed'
  >('Disconnected');

  error = signal('');

  constructor(
    private readonly conversationService: ConversationService,
  ) {
    this.createConversation();
  }

  async createConversation(): Promise<void> {
    this.status.set('Connecting');
    this.error.set('');

    try {
      const id =
        await this.conversationService.createConversation();

      this.conversationId.set(id);
      this.status.set('Disconnected');
    } catch (error) {
      console.error(error);

      this.status.set('Failed');
      this.error.set(
        error instanceof Error
          ? error.message
          : 'Failed to create conversation.',
      );
    }
  }

  async sendMessage(): Promise<void> {
    const content = this.message.trim();
    const conversationId = this.conversationId();

    if (!content || !conversationId) {
      return;
    }

    this.response.set('');
    this.userMessage.set(content);
    this.lastSequence.set(0);
    this.error.set('');
    this.status.set('Connecting');

    this.conversationService.disconnect();

    try {
      const result =
        await this.conversationService.sendMessage(
          conversationId,
          content,
        );

      this.runId.set(result.runId);
      this.message = '';

      this.connectToStream(result.runId, 0);
    } catch (error) {
      console.error(error);

      this.status.set('Failed');
      this.error.set(
        error instanceof Error
          ? error.message
          : 'Failed to send message.',
      );
    }
  }

  reconnect(): void {
    const runId = this.runId();

    if (!runId) {
      return;
    }

    const sequence =
      this.conversationService.getLastSequence();

    this.status.set('Reconnecting');

    this.connectToStream(runId, sequence);
  }

  private connectToStream(
    runId: string,
    afterSequence: number,
  ): void {
    this.status.set(
      afterSequence > 0
        ? 'Reconnecting'
        : 'Connecting',
    );

    this.conversationService.connectToStream(
      runId,
      afterSequence,
      {
        onEvent: (event) => {
          this.handleEvent(event);
        },

        onStatus: (status) => {
          if (status === 'Connected') {
            this.status.set('Connected');
          } else if (status === 'Connected (replayed)') {
            this.status.set('Connected');
          } else if (status === 'Reconnecting') {
            this.status.set('Reconnecting');
          }
        },

        onError: (message) => {
          console.error(message);

          this.status.set('Failed');
          this.error.set(message);
        },
      },
    );
  }

  private handleEvent(event: StreamEvent): void {
    /*
     * The service already performs sequence-based
     * deduplication, so the UI only processes events
     * that have not already been consumed.
     */

    this.lastSequence.set(event.sequence);

    switch (event.type) {
      case 'run.started':
        this.status.set('Connected');
        break;

      case 'token':
        this.response.update(
          current => current + (event.data ?? ''),
        );
        break;

      case 'run.completed':
        this.status.set('Completed');
        break;

      case 'run.failed':
        this.status.set('Failed');
        this.error.set(
          event.data ?? 'The run failed.',
        );
        break;
    }
  }

  ngOnDestroy(): void {
    this.conversationService.disconnect();
  }
}