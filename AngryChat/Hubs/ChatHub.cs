using Microsoft.AspNetCore.SignalR;
using AngryChat.Pages;
using Microsoft.AspNetCore.Components;

namespace BlazorServerSignalRApp.Server.Hubs
{
    public class ChatHub : Hub
    {
        public const string HubUrl = "/chathub";


        [Parameter] public EventCallback RemoveUser { get; set; }

        public async Task SendMessage(string message, string senderid, string senderunsername, DateTime messagetime, bool issent, bool isdelivered)
        {
            await Clients.All.SendAsync("ReceiveMessage", message, senderid, senderunsername, messagetime, issent , isdelivered);
            
        }
        public async Task SendClearChatSignal()
        {
            await Clients.All.SendAsync("ClearChatSignal");
        }

        public async Task SendToSpecific(string to, string message, string senderunsername)
        {
            await Clients.Client(to).SendAsync("ReceiveDirectMessage", message, senderunsername);
        }

        public override Task OnConnectedAsync()
        {
            UserHandler.ConnectedIds.Add(Context.ConnectionId);
            return base.OnConnectedAsync();
        }

        public override async Task<Task> OnDisconnectedAsync(Exception exception)
        {
            UserHandler.ConnectedIds.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
    public static class UserHandler
    {
        public static HashSet<string> ConnectedIds = new HashSet<string>();
    }
}