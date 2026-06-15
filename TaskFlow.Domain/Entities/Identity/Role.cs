using System;
using System.Collections.Generic;
using System.Text;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities.Identity
{
    public class Role : AuditableEntity, IAggregateRoot
    {
        public string Name { get;private set; }
        private Role()
        {

        }

        public Role(string name)
        {
            Name = name;
        }
    }
}
