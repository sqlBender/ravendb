namespace Raven.Studio.Plugin
{
	using Client;

	public interface IDatabase
	{
		string Address { get; set; }
		string Name { get; set; }
		IAsyncDocumentSession Session { get; set; }
	}
}