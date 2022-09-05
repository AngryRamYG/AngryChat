using Microsoft.AspNetCore.Components;
using BlazorServerSignalRApp.Server.Hubs;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Cosmos;
using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.AspNetCore.Components.Web;

namespace AngryChat.Pages
{
    public partial class Index
    {
        private HubConnection hubConnection;
        //private List<string> messages = new List<string>();
        HashSet<string> IDS = UserHandler.ConnectedIds;
        Container ConversationsContainer { get; set; }
        Container MessagesContainer { get; set; }
        Container UsersContainer { get; set; }
        List<Conversation> Conversations { get; set; } = new();

        List<Message> Messages { get; set; } = new();
        List<User> Users { get; set; } = new();
        public string NewConversationName { get; set; }
        public string? SelectedConversation { get; set; } = "None";
        public string? SelectedUserId { get; set; } = "None";
        public string? Options { get; set; } = "None";
        public string MessageString { get; set; }
        public DateTime GlobalMessageTime { get; set; }
        public static bool GlobalIsSent { get; set; } = false;
        public static bool GlobalIsDelivered { get; set; } = false;
        public string UserNameTemp { get; set; }
        public string ConnectionIDTemp { get; set; }
        public string UserName { get; set; }


        protected override async Task OnInitializedAsync()
        {
            await InitializeSignalRConnection();
            await InitializeCosmos();
            await RetrieveConversations();
            await RetrieveMessages();
            await RetrieveUsers();

            await CheckForDisconnect();
        }

        #region SignalR Code
        protected async Task CheckForDisconnect()
        {
            hubConnection.Closed += async (error) =>
            {
                await RemoveUserOnline();
            };
        }
        protected void update()
        {
            IDS = UserHandler.ConnectedIds;
        }
        private async Task InitializeSignalRConnection()
        {
            hubConnection = new HubConnectionBuilder().WithUrl(NavigationManager.ToAbsoluteUri("/chathub")).Build();

            await hubConnection.StartAsync();

            await CheckForMessagesRecieved();

            await CheckForClearChat();

        }
        protected async Task CheckForMessagesRecieved()
        {
            hubConnection.On<string, string>("ReceiveDirectMessage", async (message, senderunsername) =>
            {
                Console.WriteLine($"{senderunsername}: {message}");

                await InvokeAsync(StateHasChanged);
            });

            hubConnection.On<string, string, string, DateTime>("ReceiveMessage", async (message, senderid, username, time) =>
            {
                GlobalIsDelivered = true;

                await hubConnection.SendAsync("StatusUpdate", GlobalIsSent, GlobalIsDelivered);

                if (senderid != hubConnection.ConnectionId)
                {
                    Messages.Add(new Message()
                    {
                        UserNameString = username,
                        MessageString = message,
                        MessageTime = time,
                        IsDelivered = GlobalIsDelivered,
                        IsSent = GlobalIsSent
                    });
                }



                await InvokeAsync(StateHasChanged);

            });
        }
        protected async Task CheckForClearChat()
        {
            hubConnection.On("ClearChatSignal", async () =>
            {

                await RetrieveMessages();

                await InvokeAsync(StateHasChanged);

            });

        }
        protected async Task SendDirectMessage()
        {
            await SendSignalRDirectMessage();
            await SendCosmosDirectMessage();
        }
        protected async Task SendGroupMessage()
        {
            GlobalMessageTime = DateTime.UtcNow;
            await SendSignalRGroupMessage();
            await SendCosmosGroupMessage();
            GlobalIsSent = true;
        }
        protected async Task SendClearSignal()
        {
            await hubConnection.SendAsync("SendClearChatSignal");
        }
        private async Task SendSignalRDirectMessage()
        {
            if (hubConnection is not null)
            {
                foreach (User user in Users)
                    if (user.id == SelectedUserId)
                        await hubConnection.SendAsync("SendToSpecific", user.ConnectionID, MessageString, UserName);
            }
        }
        private async Task SendSignalRGroupMessage()
        {
            if (hubConnection is not null)
            {
                await hubConnection.SendAsync("SendMessage", MessageString, hubConnection.ConnectionId, UserName, GlobalMessageTime);
                Messages.Add(new Message()
                {
                    UserNameString = UserName,
                    MessageString = MessageString,
                    MessageTime = GlobalMessageTime
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (hubConnection is not null)
            {
                await hubConnection.DisposeAsync();
            }
        }

        protected async Task SendGroupMessageOnKeyDown(KeyboardEventArgs e)
        {
            if (e.Code == "Enter" || e.Code == "NumpadEnter")
                await SendGroupMessage();
        }
        protected async Task SendDirectMessageOnKeyDown(KeyboardEventArgs e)
        {
            if (e.Code == "Enter" || e.Code == "NumpadEnter")
                await SendDirectMessage();
        }


        #endregion


        #region CosmosDB Code
        public record Conversation
        {
            public string id => ID.ToString();
            public Guid ID { get; set; }
            public string Name { get; set; }
            public string PartitionKey { get; set; }
        }
        public record DirectMessage
        {
            public string id => ID.ToString();
            public Guid ID { get; set; }
            public string MessageString { get; set; }
            public string PartitionKey { get; set; }
            public string SenderID { get; set; }
            public string RecieverID { get; set; }
            public DateTime MessageTime { get; set; }

        }
        public record Message
        {
            public string id => ID.ToString();
            public Guid ID { get; set; }
            public string UserNameString { get; set; }
            public string MessageString { get; set; }
            public string PartitionKey { get; set; }
            public DateTime MessageTime { get; set; }
            public bool IsSent { get; set; } = GlobalIsSent;
            public bool IsDelivered { get; set; } = GlobalIsDelivered;

            public string Status
            {
                get
                {
                    if (IsDelivered)
                        return "Delivered";

                    if (IsSent)
                        return "Sent";

                    return "Sending...";
                }
            }
        }
        public record User
        {
            public string id => ID.ToString();
            public Guid ID { get; set; }
            public string PartitionKey { get; set; }
            public string ConnectionID { get; set; }
            public string Name { get; set; }
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
            UsersContainer = ChatboxDatabase.GetContainer("Users");
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
        protected async Task RetrieveUsers()
        {
            var queryable = UsersContainer.GetItemLinqQueryable<User>(requestOptions: new QueryRequestOptions())
                .Where(key => key.PartitionKey == "User")
                .OrderBy(key => key.Name);

            Users = await ToListAsync(queryable);
        }

        protected async Task SendCosmosDirectMessage()
        {
            DirectMessage directmessage = new()
            {
                ID = Guid.NewGuid(),
                MessageString = MessageString,
                SenderID = UserName,//using username because we dont have specific general id for users yet (need login system).
                RecieverID = SelectedUserId,
                PartitionKey = $"{UserName}{SelectedUserId}"
            };
            MessageString = null;
            await MessagesContainer.CreateItemAsync(directmessage);
        }
        protected async Task SendCosmosGroupMessage()
        {
            Message message = new()
            {
                ID = Guid.NewGuid(),
                UserNameString = UserName,
                MessageString = MessageString,
                PartitionKey = SelectedConversation,
                MessageTime = GlobalMessageTime
            };
            MessageString = null;

            await MessagesContainer.CreateItemAsync(message);
        }

        protected async Task AddUserOnline()
        {
            UserName = UserNameTemp;
            ConnectionIDTemp = hubConnection.ConnectionId;
            User user = new()
            {
                ID = Guid.NewGuid(),
                PartitionKey = "User",
                ConnectionID = hubConnection.ConnectionId,
                Name = UserName
            };
            await UsersContainer.CreateItemAsync(user);
            await RetrieveUsers();
        }
        public async Task RemoveUserOnline()
        {
            await RetrieveUsers();
            foreach (User user in Users)
                if (user.ConnectionID == ConnectionIDTemp)
                    UsersContainer.DeleteItemAsync<User>(user.id, new PartitionKey(user.PartitionKey));

            await RetrieveUsers();


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
        protected async Task ClearAllChat()
        {

            await RetrieveMessages();

            foreach (Message message in Messages)
                await MessagesContainer.DeleteItemAsync<Message>(message.id, new PartitionKey(message.PartitionKey));

            await RetrieveMessages();

            await SendClearSignal();
        }
        protected async Task ClearAllUsers()
        {
            await RetrieveUsers();

            foreach (User user in Users)
                await UsersContainer.DeleteItemAsync<User>(user.id, new PartitionKey(user.PartitionKey));

            await RetrieveUsers();
        }

        #endregion
    }
}