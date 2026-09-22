using Caygnus.ResumableConversation.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Caygnus.ResumableConversation.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunEvent> RunEvents => Set<RunEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Content)
                .IsRequired();

            entity.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Run)
                .WithOne(x => x.UserMessage)
                .HasForeignKey<Run>(x => x.UserMessageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Run>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(30);

            entity.Property(x => x.FailureReason)
                .HasMaxLength(2000);

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Runs)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RunEvent>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventType)
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(x => x.Text);

            entity.HasOne(x => x.Run)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new
            {
                x.RunId,
                x.Sequence
            })
            .IsUnique();
        });
    }
}