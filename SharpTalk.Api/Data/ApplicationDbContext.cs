using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Entities;

namespace SharpTalk.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Workspace> Workspaces { get; set; } = null!;
    public DbSet<WorkspaceMember> WorkspaceMembers { get; set; } = null!;
    public DbSet<Channel> Channels { get; set; } = null!;
    public DbSet<ChannelMember> ChannelMembers { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<Attachment> Attachments { get; set; } = null!;
    public DbSet<Reaction> Reactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique constraints
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        // Performance indexes for frequently queried columns
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.ChannelId);

        modelBuilder.Entity<WorkspaceMember>()
            .HasIndex(wm => wm.UserId);

        modelBuilder.Entity<WorkspaceMember>()
            .HasIndex(wm => new { wm.WorkspaceId, wm.UserId });

        modelBuilder.Entity<Channel>()
            .HasIndex(c => c.WorkspaceId);

        modelBuilder.Entity<Channel>()
            .HasIndex(c => new { c.WorkspaceId, c.Type });

        // ChannelMember Composite Key (already indexed by primary key)
        modelBuilder.Entity<ChannelMember>()
            .HasKey(cm => new { cm.ChannelId, cm.UserId });

        // Relationships
        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(wm => wm.User)
            .WithMany()
            .HasForeignKey(wm => wm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reaction>()
            .HasIndex(r => new { r.MessageId, r.UserId, r.EmojiCode }).IsUnique();

        // Attachment relationship
        modelBuilder.Entity<Attachment>()
            .HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
