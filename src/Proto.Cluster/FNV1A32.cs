namespace System.Security.Cryptography
{
    public abstract class FNV1A32 : HashAlgorithm
    {
        public new static FNV1A32 Create() => new Implementation();

        private sealed class Implementation : FNV1A32
        {
            private const uint FnvPrime = 0x01000193;

            private const uint FnvOffsetBasis = 0x811C9DC5;

            private uint _hash;

            public Implementation()
            {
                Initialize();
                HashSizeValue = 32;
            }

            public override void Initialize()
            {
                _hash = FnvOffsetBasis;
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
                        _hash ^= array[i];
                        _hash *= FnvPrime;
                    }
                }
            }

            protected override byte[] HashFinal() => BitConverter.GetBytes(_hash);
        }
    }
}