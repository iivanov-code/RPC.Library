namespace RPC.Test
{
    internal class Service : IService
    {
        public Data GetData()
        {
            return new Data();
        }
    }
}
