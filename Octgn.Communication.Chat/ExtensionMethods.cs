using Octgn.Communication.Packets;
using System.Threading.Tasks;

namespace Octgn.Communication.Chat
{
    public static class ExtensionMethods
    {
        public static ChatClientModule InitializeChat(this Client client) {
            var module = new ChatClientModule(client);
            client.Attach(module);
            return module;
        }

        public static ChatClientModule Chat(this Client client) {
            return client.GetModule<ChatClientModule>();
        }
    }
}
