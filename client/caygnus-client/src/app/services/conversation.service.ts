import { Injectable } from '@angular/core';

export interface SendMessageResponse {
    conversationId: string;
    messageId: string;
    runId: string;
}

export interface StreamEvent {
    sequence: number;
    type: string;
    data?: string;
}

interface StreamHandlers {
    onEvent: (event: StreamEvent) => void;
    onStatus: (status: string) => void;
    onError: (message: string) => void;
}

@Injectable({
    providedIn: 'root',
})
export class ConversationService {
    private readonly apiBaseUrl = 'https://localhost:7269/api';

    private eventSource: EventSource | null = null;
    private reconnectTimer: ReturnType<typeof setTimeout> | null = null;

    private currentRunId: string | null = null;
    private currentHandlers: StreamHandlers | null = null;
    private currentSequence = 0;
    private manuallyDisconnected = false;

    async createConversation(): Promise<string> {
        const response = await fetch(`${this.apiBaseUrl}/Conversation`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({}),
        });

        if (!response.ok) {
            throw new Error(
                `Failed to create conversation (${response.status}).`,
            );
        }

        const data = await response.json();

        return data.id ?? data.conversationId;
    }

    async sendMessage(
        conversationId: string,
        content: string,
    ): Promise<SendMessageResponse> {
        const response = await fetch(
            `${this.apiBaseUrl}/conversations/${conversationId}/messages`,
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ content }),
            },
        );

        if (!response.ok) {
            const message = await response.text();

            throw new Error(
                message || `Failed to send message (${response.status}).`,
            );
        }

        return response.json();
    }

    connectToStream(
        runId: string,
        afterSequence: number,
        handlers: StreamHandlers,
    ): void {
        this.disconnect();

        this.currentRunId = runId;
        this.currentHandlers = handlers;
        this.currentSequence = afterSequence;
        this.manuallyDisconnected = false;

        this.openStream(runId, afterSequence);
    }

    private openStream(runId: string, afterSequence: number): void {
        const url =
            `${this.apiBaseUrl}/runs/${runId}/stream` +
            `?after=${afterSequence}`;

        const source = new EventSource(url);

        this.eventSource = source;

        source.onopen = () => {
            this.currentHandlers?.onStatus(
                afterSequence > 0 ? 'Connected (replayed)' : 'Connected',
            );
        };

        /*
         * The backend uses named SSE events.
         *
         * event: run.started
         * event: token
         * event: run.completed
         * event: run.failed
         *
         * Therefore EventSource.onmessage is not enough.
         */
        source.addEventListener('run.started', (message) => {
            this.handleSseEvent('run.started', message);
        });

        source.addEventListener('token', (message) => {
            this.handleSseEvent('token', message);
        });

        source.addEventListener('run.completed', (message) => {
            this.handleSseEvent('run.completed', message);
        });

        source.addEventListener('run.failed', (message) => {
            this.handleSseEvent('run.failed', message);
        });

        // Keep this as a fallback in case the backend sends an unnamed event.
        source.onmessage = (message) => {
            this.handleSseEvent('message', message);
        };

        source.onerror = () => {
            source.close();

            if (this.eventSource === source) {
                this.eventSource = null;
            }

            if (this.manuallyDisconnected) {
                return;
            }

            this.currentHandlers?.onStatus('Reconnecting');

            this.scheduleReconnect();
        };
    }

    private handleSseEvent(
        type: string,
        message: MessageEvent,
    ): void {
        const event = this.parseEvent(message, type);

        if (!event) {
            return;
        }

        // Ignore duplicate or out-of-order events.
        if (event.sequence <= this.currentSequence) {
            return;
        }

        this.currentSequence = event.sequence;
        this.currentHandlers?.onEvent(event);

        if (
            event.type === 'run.completed' ||
            event.type === 'run.failed'
        ) {
            const finalStatus =
                event.type === 'run.completed'
                    ? 'Completed'
                    : 'Failed';

            this.eventSource?.close();
            this.eventSource = null;

            this.manuallyDisconnected = true;

            if (this.reconnectTimer) {
                clearTimeout(this.reconnectTimer);
                this.reconnectTimer = null;
            }

            this.currentHandlers?.onStatus(finalStatus);
        }
    }

    private scheduleReconnect(): void {
        if (this.reconnectTimer || !this.currentRunId) {
            return;
        }

        this.reconnectTimer = setTimeout(() => {
            this.reconnectTimer = null;

            if (
                this.manuallyDisconnected ||
                !this.currentRunId ||
                !this.currentHandlers
            ) {
                return;
            }

            /*
             * IMPORTANT:
             * Reconnect from the latest successfully processed sequence.
             */
            this.openStream(
                this.currentRunId,
                this.currentSequence,
            );
        }, 1000);
    }

    private parseEvent(
        message: MessageEvent,
        fallbackType: string,
    ): StreamEvent | null {
        try {
            const parsed = JSON.parse(message.data);

            const sequence = Number(
                parsed.sequence ??
                parsed.seq ??
                message.lastEventId ??
                0,
            );

            if (!sequence) {
                return null;
            }

            return {
                sequence,
                type: parsed.type ?? parsed.eventType ?? fallbackType,
                data:
                    typeof parsed.data === 'string'
                        ? parsed.data
                        : parsed.content ??
                        parsed.token ??
                        undefined,
            };
        } catch {
            return null;
        }
    }

    disconnect(clearReconnect = true): void {
        this.manuallyDisconnected = true;

        this.eventSource?.close();
        this.eventSource = null;

        if (clearReconnect && this.reconnectTimer) {
            clearTimeout(this.reconnectTimer);
            this.reconnectTimer = null;
        }

        if (clearReconnect) {
            this.currentRunId = null;
            this.currentHandlers = null;
        }
    }

    getLastSequence(): number {
        return this.currentSequence;
    }
}