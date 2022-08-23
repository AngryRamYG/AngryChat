using Microsoft.AspNetCore.Components;
using BlazorServerSignalRApp.Server.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Cosmos;
using AngryChat.CosmosDatabase;
using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;

namespace AngryChat.Pages
{
    public partial class Index
    {
        #region SignalR Code
        //private HubConnection? hubConnection;
        //private List<string> messages = new List<string>();
        //private string? Sender;
        //private string? Message;
        //private string? RecieverID;
        //HashSet<string> IDS = UserHandler.ConnectedIds;
        //protected void update()
        //{
        //    IDS = UserHandler.ConnectedIds;
        //}

        protected override async Task OnInitializedAsync()
        {
            await InitializeCosmos();
            await RetrieveConversations();

        //    hubConnection = new HubConnectionBuilder().WithUrl(NavigationManager.ToAbsoluteUri("/chathub")).Build();
        //    hubConnection.On<string, string, string, string>("ReceiveMessage", (sender, message, recieverid, senderid) =>
        //    {
        //        Console.WriteLine("recieve function");
        //        if (recieverid == hubConnection.ConnectionId || senderid == hubConnection.ConnectionId)
        //        {
        //            var encodedMsg = "";
        //            if (recieverid == hubConnection.ConnectionId)
        //                encodedMsg = $"{sender}({senderid}): {message}";
        //            else
        //                encodedMsg = $"You -> {recieverid}: {message}";
        //            messages.Add(encodedMsg);
        //            InvokeAsync(StateHasChanged);
        //        }
        //    });
        //    await hubConnection.StartAsync();
        }

        //private async Task Send()
        //{
        //    if (hubConnection is not null)
        //    {
        //        Console.WriteLine("send function");
        //        await hubConnection.SendAsync("SendMessage", Sender, Message, RecieverID, hubConnection.ConnectionId);
        //    }
        //}

        //public bool IsConnected => hubConnection?.State == HubConnectionState.Connected;
        //public async ValueTask DisposeAsync()
        //{
        //    if (hubConnection is not null)
        //    {
        //        Console.WriteLine("Dispose function called");
        //        await hubConnection.DisposeAsync();
        //    }
        //}

        #endregion

        Container ConversationsContainer { get; set; }
        Container MessagesContainer { get; set; }
        List<Conversation> Conversations { get; set; } = new List<Conversation>();
        List<Message> Messages { get; set; } = new List<Message>();

        public cosmos createCosmos = new();
        public string NewConversationName { get; set; }
        public string SelectedConversation { get; set; }
        public string MessageString { get; set; }

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
        protected async Task SendMessage()
        {
            Message message = new()
            {
                ID = Guid.NewGuid(),
                MessageString = MessageString,
                PartitionKey = SelectedConversation
            };


            await MessagesContainer.CreateItemAsync(message);
            MessageString = null;
            await RetrieveMessages();
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