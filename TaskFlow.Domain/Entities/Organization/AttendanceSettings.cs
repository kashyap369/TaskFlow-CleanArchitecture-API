using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Organization;

/// <summary>
/// Per-organization attendance configuration. An organization without a row uses the defaults.
/// Kept apart from the organization row so attendance can grow (schedules, zones) without
/// touching the organization table.
/// </summary>
public sealed class AttendanceSettings : AuditableEntity, IAggregateRoot
{
    public const string DefaultTimeZone = "UTC";
    public const int DefaultAutoCloseAfterHours = 16;
    public const int DefaultPhotoRetentionDays = 90;

    public int OrganizationId { get; private set; }
    // IANA name. The work date of a punch is the calendar day in this time zone.
    public string TimeZone { get; private set; } = DefaultTimeZone;
    public int AutoCloseAfterHours { get; private set; } = DefaultAutoCloseAfterHours;
    public int PhotoRetentionDays { get; private set; } = DefaultPhotoRetentionDays;
    public int UpdatedByUserId { get; private set; }

    private AttendanceSettings() { }

    public AttendanceSettings(int organizationId, string timeZone, int autoCloseAfterHours, int photoRetentionDays,
        int updatedByUserId)
    {
        if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));
        OrganizationId = organizationId;
        Apply(timeZone, autoCloseAfterHours, photoRetentionDays, updatedByUserId);
    }

    public void Update(string timeZone, int autoCloseAfterHours, int photoRetentionDays, int updatedByUserId)
    {
        Apply(timeZone, autoCloseAfterHours, photoRetentionDays, updatedByUserId);
        MarkAsUpdated();
    }

    private void Apply(string timeZone, int autoCloseAfterHours, int photoRetentionDays, int updatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(timeZone)) throw new ArgumentException("Time zone is required.", nameof(timeZone));
        if (autoCloseAfterHours is < 1 or > 24) throw new ArgumentOutOfRangeException(nameof(autoCloseAfterHours));
        if (photoRetentionDays is < 7 or > 3650) throw new ArgumentOutOfRangeException(nameof(photoRetentionDays));
        if (updatedByUserId <= 0) throw new ArgumentOutOfRangeException(nameof(updatedByUserId));

        TimeZone = timeZone.Trim();
        AutoCloseAfterHours = autoCloseAfterHours;
        PhotoRetentionDays = photoRetentionDays;
        UpdatedByUserId = updatedByUserId;
    }
}
