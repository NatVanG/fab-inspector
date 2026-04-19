namespace FabInspector.ClientLibrary.Output
{
    internal interface IResultOutputWriter
    {
        Task WriteAsync(OutputContext context);
    }
}
