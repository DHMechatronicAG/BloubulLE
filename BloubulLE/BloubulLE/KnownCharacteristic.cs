using System;

namespace DH.BloubulLE
{
    public struct KnownCharacteristic
    {
        public String Name { get; }
        public Guid Id { get; }

        public KnownCharacteristic(String name, Guid id)
        {
            this.Name = name;
            this.Id = id;
        }
    }
}