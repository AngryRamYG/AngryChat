using Microsoft.Azure.Cosmos;

namespace AngryChat.CosmosDatabase
{
    public class cosmos
    {
        //Database database;

        //public  cosmos()
        //{
        //     configure();
        //}
        //public async Task configure()
        //{
        //    using CosmosClient client = new("AccountEndpoint=https://chatboxdatabase.documents.azure.com:443/;AccountKey=eD338Swm8kcukDwlvq6tuUbYJQNobJhC3CdiOQWaEayqGkneAzYNAnxF0GHID5LhAnHKZtmunBVGtsuEVnB9ug==;");
        //    // Database reference with creation if it does not already exist
        //     database = await client.CreateDatabaseIfNotExistsAsync(
        //        id: "ChatApp"
        //    );
        //}
        //public async Task<Container> MessageContainer()
        //{
            
        //    // Container reference with creation if it does not alredy exist
        //    Container messagesContainer = await database.CreateContainerIfNotExistsAsync(
        //        id: "Messages",
        //        partitionKeyPath: "/PartitionKey",
        //        throughput: 400
        //    );

        //    return messagesContainer;



        //}     
        //public async Task<Container> MessageUser()
        //{

        //    Container usersContainer = await database.CreateContainerIfNotExistsAsync(
        //          id: "Users",
        //          partitionKeyPath: "/PartitionKey",
        //          throughput: 400
        //      );
        //    return usersContainer;
        //}



    }
}
