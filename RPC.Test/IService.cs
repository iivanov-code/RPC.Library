using System.Threading.Tasks;

namespace RPC.Test
{
    internal interface IService
    {
        Data GetData();

        Task<Data> GetDataAsync();

        Data Echo(Data data);

        void Notify(string message);
    }
}
