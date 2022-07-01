namespace SkyriseMini.Tests;

public delegate Task Ping(object handle, string name);

public delegate Task<object> Activate(string id);