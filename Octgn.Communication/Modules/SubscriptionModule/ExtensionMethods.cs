namespace Octgn.Communication.Modules.SubscriptionModule
{
    public static class ExtensionMethods
    {
        public static ClientSubscriptionModule InitializeSubscriptionModule(this Client client) {
            var module = new ClientSubscriptionModule(client);
            client.Attach(module);
            return module;
        }

        public static ClientSubscriptionModule Subscription(this Client client) {
            return client.GetModule<ClientSubscriptionModule>();
        }
    }
}
