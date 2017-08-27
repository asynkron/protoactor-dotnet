namespace System.Security.Cryptography
{
    public abstract class FNV1A32 : HashAlgorithm
    {
        protected FNV1A32() { }

        public new static FNV1A32 Create()
        {
            return new Implementation();
        }

        private sealed class Implementation : FNV1A32
        {
            private const uint fnvPrime = 0x01000193;

            private const uint fnvOffsetBasis = 0x811C9DC5;

            private uint hash;

            public Implementation()
            {
                this.Initialize();
                this.HashSizeValue = 32;
            }

            public override void Initialize()
            {
                this.hash = fnvOffsetBasis;
            }

            protected override void HashCore(byte[] array, int ibStart, int cbSize)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                for (var i = ibStart; i < cbSize; i++)
                {
                    unchecked
                    {
                        this.hash ^= array[i];
                        this.hash *= fnvPrime;
                    }
                }
            }

            protected override byte[] HashFinal()
            {
                return BitConverter.GetBytes(this.hash);
            }
        }
    }
}