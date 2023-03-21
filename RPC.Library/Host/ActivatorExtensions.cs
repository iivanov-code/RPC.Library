using System.Linq;
using System.Reflection;

namespace NetworkCommunicator.Host
{
    internal static class ActivatorExtensions
    {
        public static T CreateInstance<T>(params object[] parameters)
        {
            ConstructorInfo ctr = typeof(T).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(x => x.GetParameters().Length == parameters.Length)
                .Single();

            return (T)ctr.Invoke(parameters);
        }
    }
}