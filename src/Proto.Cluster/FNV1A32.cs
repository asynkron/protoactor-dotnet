namespace System.Security.Cryptography
{
    public abstract class FNV1A32 : HashAlgorithm
    {
        protected FNV1A32() { }

#if NETSTANDARD2_0 || NET452
        public new static FNV1A32 Create()
#else
        public static FNV1A32 Create()
#endif
        {
            return new Implementation();
        }

        private sealed class Implementation : FNV1A32
        {
            private const uint FnvPrime = 0x01000193;

            private const uint FnvOffsetBasis = 0x811C9DC5;

            private uint _hash;

            public Implementation()
            {
                this.Initialize();
#if NETSTANDARD2_0 || NET452
                this.HashSizeValue = 32;
#endif
            }

            public override void Initialize()
            {
                this._hash = FnvOffsetBasis;
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
                        this._hash ^= array[i];
                        this._hash *= FnvPrime;
                    }
                }
            }

            protected override byte[] HashFinal()
            {
                return BitConverter.GetBytes(this._hash);
            }
        }
    }
}
