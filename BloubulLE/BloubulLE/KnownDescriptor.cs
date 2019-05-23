using System;

namespace DH.BloubulLE
{
    public struct KnownDescriptor
    {
        public String Name { get; }
        public Guid Id { get; }

        public KnownDescriptor(String name, Guid id)
        {
            this.Name = name;
            this.Id = id;
        }
    }
}