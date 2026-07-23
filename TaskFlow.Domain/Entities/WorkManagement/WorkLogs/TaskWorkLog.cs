using System;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.WorkManagement.WorkLogs
{
    /// <summary>
    /// One work session of one user on one task — the raw data
    /// behind time tracking and the duration-based reports
    /// (member/team time per task, project timelines).
    /// Either started live (StartNew) and stopped later, or
    /// logged manually after the fact (LogManual).
    /// </summary>
    public class TaskWorkLog : AuditableEntity, IAggregateRoot
    {
        public int TaskId { get; private set; }

        public int UserId { get; private set; }

        public DateTime StartedAt { get; private set; }

        public DateTime? EndedAt { get; private set; }

        public string Notes { get; private set; }

        public bool IsRunning =>
            !EndedAt.HasValue;

        public TimeSpan? Duration =>
            EndedAt.HasValue
                ? EndedAt.Value - StartedAt
                : null;

        protected TaskWorkLog()
        {
        }

        private TaskWorkLog(
            int taskId,
            int userId,
            DateTime startedAt,
            DateTime? endedAt,
            string notes)
        {
            if (taskId <= 0)
                throw new ArgumentException(
                    "TaskId is required.",
                    nameof(taskId));

            if (userId <= 0)
                throw new ArgumentException(
                    "UserId is required.",
                    nameof(userId));

            TaskId = taskId;
            UserId = userId;
            StartedAt = startedAt;
            EndedAt = endedAt;
            Notes = notes?.Trim();
        }

        /// <summary>
        /// Starts a live work session (timer) now.
        /// </summary>
        public static TaskWorkLog StartNew(
            int taskId,
            int userId,
            string notes = null)
        {
            return new TaskWorkLog(
                taskId,
                userId,
                DateTime.UtcNow,
                null,
                notes);
        }

        /// <summary>
        /// Records a finished work session after the fact.
        /// </summary>
        public static TaskWorkLog LogManual(
            int taskId,
            int userId,
            DateTime startedAt,
            DateTime endedAt,
            string notes = null)
        {
            if (endedAt <= startedAt)
                throw new ArgumentException(
                    "End time must be after start time.");

            if (endedAt > DateTime.UtcNow)
                throw new ArgumentException(
                    "End time cannot be in the future.");

            return new TaskWorkLog(
                taskId,
                userId,
                startedAt,
                endedAt,
                notes);
        }

        public void Stop(string notes = null)
        {
            if (!IsRunning)
                return;

            EndedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(notes))
                Notes = notes.Trim();

            MarkAsUpdated();
        }

        public void UpdateNotes(string notes)
        {
            Notes = notes?.Trim();

            MarkAsUpdated();
        }
    }
}
