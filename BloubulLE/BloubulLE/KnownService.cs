using System;

namespace DH.BloubulLE
{
    public struct KnownService
    {
        public String Name { get; }
        public Guid Id { get; }

        public KnownService(String name, Guid id)
        {
            this.Name = name;
            this.Id = id;
        }
    }
}