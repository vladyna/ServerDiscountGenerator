using System.Net;
using System.Net.Sockets;
using DiscountServer.Data;
using DiscountServer.Protocol;

SQLitePCL.Batteries.Init();

#region Setup
var dbPath = Environment.GetEnvironmentVariable("DISCOUNT_DB_PATH") ?? Path.Combine(AppContext.BaseDirectory, "discounts.db");
var db = new DiscountRepository(dbPath);
var handler = new TcpMessageHandler(db);
var listener = new TcpListener(IPAddress.Loopback, 5000);
listener.Start();
Console.WriteLine($"TCP Discount server running on port 5000... DB: {dbPath}");
#endregion

#region AcceptLoop
while (true)
{
    var client = await listener.AcceptTcpClientAsync();
    _ = Task.Run(() => handler.HandleClientAsync(client));
}
#endregion