namespace TaskFlow.Domain.Enums.Meetings;

public enum MeetingStatus { Draft = 1, Scheduled = 2, Live = 3, Ended = 4, Cancelled = 5 }
public enum MeetingAccessLevel { Host = 1, CoHost = 2, Participant = 3, Viewer = 4 }
public enum MeetingParticipantState { Invited = 1, Admitted = 2, Revoked = 3, Denied = 4, Removed = 5 }
public enum MeetingAccessLinkMode { PrivateInvitation = 1, Reusable = 2 }
public enum MeetingGuestDecisionKind { Admitted = 1, Denied = 2, Revoked = 3, Removed = 4 }
public enum MeetingAssetScanStatus { Pending = 1, Clean = 2, Rejected = 3, Failed = 4 }
public enum MeetingRecordingStatus { PendingConsent = 1, Starting = 2, Recording = 3, Processing = 4, Ready = 5, Failed = 6 }
public enum MeetingRecordingConsentStatus { Pending = 1, Accepted = 2, Declined = 3, TimedOut = 4 }
