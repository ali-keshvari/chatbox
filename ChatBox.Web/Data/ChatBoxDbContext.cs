using Microsoft.EntityFrameworkCore;
using ChatBox.Web.Models.Entities;

namespace ChatBox.Web.Data;

public class ChatBoxDbContext : DbContext
{
    public ChatBoxDbContext(DbContextOptions<ChatBoxDbContext> options) : base(options)
    {
    }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Conversation Configuration
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(250).IsRequired();
            entity.Property(e => e.ModelName).HasMaxLength(100);
            entity.HasIndex(e => e.UpdatedAt);

            // One Conversation has many Messages, cascade delete
            entity.HasMany(e => e.Messages)
                  .WithOne(m => m.Conversation)
                  .HasForeignKey(m => m.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessage Configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.CreatedAt);

            // One Message has many Attachments, cascade delete
            entity.HasMany(e => e.Attachments)
                  .WithOne(a => a.ChatMessage)
                  .HasForeignKey(a => a.ChatMessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // FileAttachment Configuration
        modelBuilder.Entity<FileAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(260).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.FileExtension).HasMaxLength(20);
            entity.HasIndex(e => e.ChatMessageId);
        });
    }
}
