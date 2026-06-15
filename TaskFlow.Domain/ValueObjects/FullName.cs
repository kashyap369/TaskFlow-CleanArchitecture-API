using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Domain.ValueObjects
{
    public class FullName : IEquatable<FullName>
    {
        public string FirstName { get; }
        public string LastName { get; }
        public string DisplayName => $"{FirstName} {LastName}";

        private FullName()
        {
            
        }

        public FullName(string firstName,string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName))
            {
                throw new ArgumentNullException("First name is required");
            }
            if (string.IsNullOrWhiteSpace(lastName))
            {
                throw new ArgumentNullException("Last name is required");
            }

            FirstName = firstName.Trim();
            LastName = lastName.Trim();

        }
        public bool Equals(FullName other)
        {
            if (other is null)
                return false;

            return FirstName == other.FirstName &&
                   LastName == other.LastName;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FullName);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FirstName, LastName);
        }

        public static bool operator ==(FullName left, FullName right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(FullName left, FullName right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
