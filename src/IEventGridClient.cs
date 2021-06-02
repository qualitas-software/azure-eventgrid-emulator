using Microsoft.Azure.EventGrid.Models;
using System.Threading.Tasks;

namespace Qs.EventGrid.Emulator
{
    public interface IEventGridClient
    {
        Task SendEventAsync(string subscriberFunctionName, params EventGridEvent[] events);
    }
}
