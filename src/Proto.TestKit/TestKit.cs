namespace Proto.TestKit
{
    public static class TestKit
    {
        static TestKit()
        {
            System = new ActorSystem();
        }
        public static ActorSystem System { get; }
    }
}