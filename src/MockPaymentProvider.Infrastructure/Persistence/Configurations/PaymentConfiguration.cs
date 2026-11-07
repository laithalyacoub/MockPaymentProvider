using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MockPaymentProvider.Domain.Entities;

namespace MockPaymentProvider.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.TransactionId)
            .HasMaxLength(50)
            .IsRequired();
        builder.HasIndex(p => p.TransactionId).IsUnique();

        builder.Property(p => p.MobileNumber)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasColumnType("decimal(10,2)");

        // stored in the table as the text SUCCESS or FAILED
        builder.Property(p => p.Status)
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion(
                status => status.ToString().ToUpperInvariant(),
                text => Enum.Parse<PaymentStatus>(text, true));

        builder.Property(p => p.Message)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnType("datetime2");

        // The database builds the reference itself. Only successful payments get one.
        builder.Property(p => p.PaymentReference)
            .HasMaxLength(30)
            .HasComputedColumnSql(
                "CASE WHEN [Status] = 'SUCCESS' THEN 'PAY-' + CAST(50000 + [Id] AS nvarchar(20)) ELSE NULL END",
                stored: true);
    }
}
