using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Platform
{
    /// <summary>
    /// Platform-wide configuration, owned by the Admin portal.
    ///
    /// This is a <b>singleton row</b>: exactly one record exists, with
    /// <see cref="SingletonId"/> as its key, created by the seeder on
    /// first startup. Modelling it as a table rather than
    /// appsettings.json is deliberate — these are values an admin
    /// changes at runtime through the UI, not deployment configuration.
    ///
    /// Kept deliberately small. Every field here has to be honoured by
    /// code somewhere; a setting nothing reads is worse than no setting
    /// at all, because the UI implies it works.
    /// </summary>
    public class PlatformSetting : AuditableEntity
    {
        /// <summary>
        /// The only id this table ever holds. Callers read and write
        /// the singleton rather than querying by an arbitrary key.
        /// </summary>
        public const int SingletonId = 1;

        public string ApplicationName { get; private set; }

        public string? SupportEmail { get; private set; }

        /// <summary>
        /// When false, <c>POST /auth/register</c> is closed — useful
        /// for a private deployment or while responding to abuse.
        /// Enforced in the register handler.
        /// </summary>
        public bool RegistrationOpen { get; private set; }

        /// <summary>
        /// When true, every non-admin request is refused with 503.
        /// Admins keep working so the platform can be inspected while
        /// it is closed.
        /// </summary>
        public bool MaintenanceMode { get; private set; }

        /// <summary>
        /// Shown as a banner by the client while set. Null clears it.
        /// </summary>
        public string? MaintenanceMessage { get; private set; }

        protected PlatformSetting()
        {
        }

        public PlatformSetting(
            string applicationName,
            string? supportEmail = null,
            bool registrationOpen = true,
            bool maintenanceMode = false,
            string? maintenanceMessage = null)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
                throw new ArgumentException(
                    "Application name is required.",
                    nameof(applicationName));

            ApplicationName = applicationName.Trim();
            SupportEmail = supportEmail?.Trim();
            RegistrationOpen = registrationOpen;
            MaintenanceMode = maintenanceMode;
            MaintenanceMessage = maintenanceMessage?.Trim();
        }

        public void Update(
            string applicationName,
            string? supportEmail,
            bool registrationOpen,
            bool maintenanceMode,
            string? maintenanceMessage)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
                throw new ArgumentException(
                    "Application name is required.",
                    nameof(applicationName));

            ApplicationName = applicationName.Trim();
            SupportEmail = supportEmail?.Trim();
            RegistrationOpen = registrationOpen;
            MaintenanceMode = maintenanceMode;

            // An empty string is the UI clearing the banner; store it
            // as null so "no message" has exactly one representation.
            MaintenanceMessage =
                string.IsNullOrWhiteSpace(maintenanceMessage)
                    ? null
                    : maintenanceMessage.Trim();

            MarkAsUpdated();
        }
    }
}
