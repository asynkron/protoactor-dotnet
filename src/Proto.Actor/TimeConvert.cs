using System;

namespace Proto
{
    //TODO: do we really need this, this is _very_ Go style
    public static class TimeConvert
    {
        public static long ToNanoseconds(double milliseconds) => Convert.ToInt64(milliseconds * 1000000);

        public static long ToMilliseconds(long nanoseconds) => nanoseconds / 1000000;
    }
}