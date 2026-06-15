using System;

namespace TaskFlow.Domain.ValueObjects
{
    public sealed class PhoneNumber : IEquatable<PhoneNumber>
    {
        public string Value { get; }

        private PhoneNumber()
        {
        }

        public PhoneNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Phone number is required.");

            value = value.Trim();

            if (!IsValid(value))
                throw new ArgumentException("Invalid phone number.");

            Value = value;
        }

        private static bool IsValid(string phoneNumber)
        {
            if (phoneNumber.Length < 10 || phoneNumber.Length > 15)
                return false;

            foreach (char c in phoneNumber)
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        public bool Equals(PhoneNumber other)
        {
            if (other is null)
                return false;

            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PhoneNumber);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(
            PhoneNumber left,
            PhoneNumber right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(
            PhoneNumber left,
            PhoneNumber right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}