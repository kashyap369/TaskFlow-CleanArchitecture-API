using System;

namespace TaskFlow.Domain.ValueObjects
{
    public sealed class Email : IEquatable<Email>
    {
        public string Value { get; }

        private Email()
        {
        }

        public Email(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Email is required.");

            value = value.Trim().ToLowerInvariant();

            if (!IsValid(value))
                throw new ArgumentException("Invalid email format.");

            Value = value;
        }

        private static bool IsValid(string email)
        {
            try
            {
                var mailAddress = new System.Net.Mail.MailAddress(email);

                return mailAddress.Address == email;
            }
            catch
            {
                return false;
            }
        }

        public bool Equals(Email other)
        {
            if (other is null)
                return false;

            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Email);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(Email left, Email right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Email left, Email right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}