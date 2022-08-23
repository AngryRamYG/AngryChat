using Microsoft.AspNetCore.Components;
using BlazorServerSignalRApp.Server.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Cosmos;
using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;

namespace AngryChat.Pages
{
    public partial class Index
    {
        #region SignalR Code
        private HubConnection? hubConnection;
        //private List<string> messages = new List<string>();
        HashSet<string> IDS = UserHandler.ConnectedIds;
        Container ConversationsContainer { get; set; }
        Container MessagesContainer { get; set; }
        List<Conversation> Conversations { get; set; } = new List<Conversation>();
        List<Message> Messages { get; set; } = new List<Message>();

        public string NewConversationName { get; set; }
        
        public string? SelectedConversation { get; set; } = "None";
        public string MessageString { get; set; }
        public string UserName { get; set; }


        protected void update()
        {
            IDS = UserHandler.ConnectedIds;
        }

        protected override async Task OnInitializedAsync()
        {
            await InitializeCosmos();
            await RetrieveConversations();
            await RetrieveMessages();

            hubConnection = new HubConnectionBuilder().WithUrl(NavigationManager.ToAbsoluteUri("/chathub")).Build();
            hubConnection.On<string, string, string>("ReceiveMessage", async (message, senderid, Username) =>
            {
                Console.WriteLine("Signal R RecieveMessage func called");
                if (senderid != hubConnection.ConnectionId)
                {
                    message = $"[{@UserName}]: {@message}";
                    Messages.Add(new Message() { MessageString = message });

                    await RetrieveMessages();
                }

                await InvokeAsync(StateHasChanged);
            });
            await hubConnection.StartAsync();
        }

        protected async Task SendMessage()
        {
            await SendSignalRMessage();
            await SendCosmosMessage();
            await RetrieveMessages();
        }


        private async Task SendSignalRMessage()
        {
            if (hubConnection is not null)
            {
                Console.WriteLine("Signal R Send func called");
                await hubConnection.SendAsync("SendMessage", MessageString, hubConnection.ConnectionId, UserName);
                
            }
        }

        public bool IsConnected => hubConnection?.State == HubConnectionState.Connected;
        public async ValueTask DisposeAsync()
        {
            if (hubConnection is not null)
            {
                Console.WriteLine("Dispose function called");
                await hubConnection.DisposeAsync();
            }
        }

        #endregion



        // C# record representing an item in the container

        public record Conversation
        {
            public string id => ID.ToString();
            public Guid ID { get; set; }
            public string Name { get; set; }
            public string PartitionKey { get; set; }
        }
        public record Message
        {
            public string id => ID.ToString();
            public Guid ID { get; set; }
            public string UserNameString { get; set; }
            public string MessageString { get; set; }
            public string PartitionKey { get; set; }
        }


        #region Cosmos Common
        public async Task<List<T>> ToListAsync<T>(IQueryable<T> query)
        {
            List<T> list = new();

            using FeedIterator<T> setIterator = query.ToFeedIterator();

            while (setIterator.HasMoreResults)
                foreach (var item in await setIterator.ReadNextAsync())
                    list.Add(item);

            return list;
        }

        #endregion

        protected async Task InitializeCosmos()
        {
            CosmosClient ChatboxClient = new CosmosClient("AccountEndpoint=https://chatboxdatabase.documents.azure.com:443/;AccountKey=eD338Swm8kcukDwlvq6tuUbYJQNobJhC3CdiOQWaEayqGkneAzYNAnxF0GHID5LhAnHKZtmunBVGtsuEVnB9ug==;");
            Database ChatboxDatabase = ChatboxClient.GetDatabase("ChatBox");

            ConversationsContainer = ChatboxDatabase.GetContainer("Conversations");
            MessagesContainer = ChatboxDatabase.GetContainer("Messages");
        }

        protected async Task RetrieveConversations()
        {
            var queryable = ConversationsContainer.GetItemLinqQueryable<Conversation>(requestOptions: new QueryRequestOptions())
                .Where(key => key.PartitionKey == "Conversation")
                .OrderBy(key => key.Name);

            Conversations = await ToListAsync(queryable);

        }
        protected async Task RetrieveMessages()
        {
            var queryable = MessagesContainer.GetItemLinqQueryable<Message>(requestOptions: new QueryRequestOptions())
                .Where(key => key.PartitionKey == SelectedConversation)
                .OrderBy(key => key.PartitionKey);

            Messages = await ToListAsync(queryable);
        }
        protected async Task SendCosmosMessage()
        {
            Message message = new()
            {
                ID = Guid.NewGuid(),
                UserNameString = UserName,
                MessageString = MessageString,
                PartitionKey = SelectedConversation
            };
            MessageString = null;

            await MessagesContainer.CreateItemAsync(message);
        }

        protected async Task NewConversation()
        {
            Conversation conversation = new()
            {
                ID = Guid.NewGuid(),
                Name = NewConversationName,
                PartitionKey = "Conversation"
            };

            await ConversationsContainer.CreateItemAsync(conversation);
            NewConversationName = null;
            await RetrieveConversations();
        }
        protected async Task ClearChat()
        {
            foreach (Message message in Messages)
                await MessagesContainer.DeleteItemAsync<Message>(message.id, new PartitionKey(message.PartitionKey));

            await RetrieveMessages();
        }
    }
}