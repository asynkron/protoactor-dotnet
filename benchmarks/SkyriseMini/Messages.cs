namespace TestRunner.Contract;

public record RunMessagingTest(int Parallelism, int DurationInSeconds);

public record RunActivationTest(int ActivationCount, int Parallelism);