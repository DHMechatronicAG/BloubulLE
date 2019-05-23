using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DH.BloubulLE.Contracts;
using DH.BloubulLE.EventArgs;

namespace DH.BloubulLE.Tests.BloubulLE.Mocks
{
    public class CharacteristicMock : CharacteristicBase
    {
        public CharacteristicMock(IService service = null) : base(service)
        {
        }

        public CharacteristicPropertyType MockPropterties { get; set; }
        public Byte[] MockValue { get; set; }
        public List<WriteOperation> WriteHistory { get; } = new List<WriteOperation>();
        public override Guid Id { get; } = Guid.Empty;
        public override String Uuid { get; } = String.Empty;
        public override Byte[] Value => this.MockValue;

        public override CharacteristicPropertyType Properties => this.MockPropterties;


        public override event EventHandler<CharacteristicUpdatedEventArgs> ValueUpdated;

        protected override Task<IList<IDescriptor>> GetDescriptorsNativeAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task<Byte[]> ReadNativeAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task<Boolean> WriteNativeAsync(Byte[] data, CharacteristicWriteType writeType)
        {
            this.WriteHistory.Add(new WriteOperation(data, writeType));
            return Task.FromResult(true);
        }

        protected override Task StartUpdatesNativeAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task StopUpdatesNativeAsync()
        {
            throw new NotImplementedException();
        }

        public class WriteOperation
        {
            public WriteOperation(Byte[] value, CharacteristicWriteType writeType)
            {
                this.Value = value;
                this.WriteType = writeType;
            }

            public Byte[] Value { get; }
            public CharacteristicWriteType WriteType { get; }
        }
    }
}