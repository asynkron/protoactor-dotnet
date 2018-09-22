// -----------------------------------------------------------------------
//  <copyright file="DynamoDBHelper.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Proto.Persistence.DynamoDB
{
    public class DynamoDBHelper
    {
        private readonly IAmazonDynamoDB _dynamoDBClient;

        private DynamoDBHelper(IAmazonDynamoDB dynamoDBClient)
        {
            if (dynamoDBClient == null) throw new ArgumentNullException("dynamoDBClient");
            _dynamoDBClient = dynamoDBClient;
        }

        /// <sumary>
        /// Checks if table for events with given names exists. If not it creates it.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when DynamoDB table already exists, but with different properties OR when table is being deleted.</exception>
        /// <exception cref="Amazon.DynamoDBv2.AmazonDynamoDBException">Thrown when timeout occurs when getting table status from AWS.</exception>
        public static async Task CheckCreateEventsTable(
            IAmazonDynamoDB dynamoDBClient, DynamoDBProviderOptions options,
            int initialReadCapacityUnits, int initialWriteCapacityUnits)
        {
            var instance = new DynamoDBHelper(dynamoDBClient);
            await instance.CheckCreateTable(
                options.EventsTableName, options.EventsTableHashKey, options.EventsTableSortKey,
                initialReadCapacityUnits, initialWriteCapacityUnits
            );
        }

        /// <sumary>
        /// Checks if table for snapshots with given names exists. If not it creates it.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when DynamoDB table already exists, but with different properties OR when table is being deleted.</exception>
        /// <exception cref="Amazon.DynamoDBv2.AmazonDynamoDBException">Thrown when timeout occurs when getting table status from AWS.</exception>
        public static async Task CheckCreateSnapshotsTable(
            IAmazonDynamoDB dynamoDBClient, DynamoDBProviderOptions options,
            int initialReadCapacityUnits, int initialWriteCapacityUnits)
        {
            var instance = new DynamoDBHelper(dynamoDBClient);
            await instance.CheckCreateTable(
                options.SnapshotsTableName, options.SnapshotsTableHashKey, options.SnapshotsTableSortKey,
                initialReadCapacityUnits, initialWriteCapacityUnits
            );
        }

        private async Task CheckCreateTable(string tableName, string partitionKey, string sortKey, int readCapacityUnits, int writeCapacityUnits)
        {
            var existingTable = await IsTableCreated(tableName, true);

            if (existingTable.Created == true)
            {
                CheckTableKeys(existingTable.TableDesc, partitionKey, sortKey);
            }
            else
            {
                var res = await CreateTable(tableName, partitionKey, sortKey, readCapacityUnits, writeCapacityUnits);
                if (res.TableStatus != "ACTIVE")
                {
                    await Task.Delay(2000);
                    await IsTableCreated(tableName, false);
                }
            }
        }

        private async Task<(bool Created, TableDescription TableDesc)> IsTableCreated(string tableName, bool falseAccepted, int retryNumber = 0)
        {
            try
            {
                var res = await _dynamoDBClient.DescribeTableAsync(new DescribeTableRequest
                {
                    TableName = tableName
                });

                if (res.Table.TableStatus == "ACTIVE") {
                    return (true, res.Table);
                }
                if (res.Table.TableStatus == "DELETING") {
                    throw new InvalidOperationException(String.Format("DynamoDB table ${0} is being deleted.", tableName));
                }
                // if CREATING or UPDATING just let it thru and recursevly try again
            }
            catch (ResourceNotFoundException)
            {
                if (falseAccepted) {
                    return (false, null);
                }
            }

            if (retryNumber > 10) {
                // We've been waiting for 20s already. Lets throw exception.
                throw new AmazonDynamoDBException(String.Format("Failed to get status for DynamoDB table ${0}.", tableName));
            }

            await Task.Delay(2000); // Wait 2 seconds.
            return await IsTableCreated(tableName, falseAccepted, retryNumber + 1);
        }

        private async Task<TableDescription> CreateTable(string tableName, string partitionKey, string sortKey, int readCapacityUnits, int writeCapacityUnits)
        {
            var request = new CreateTableRequest
            {
                AttributeDefinitions = new List<AttributeDefinition>()
                {
                    new AttributeDefinition
                    {
                        AttributeName = partitionKey,
                        AttributeType = "S"
                    },
                    new AttributeDefinition
                    {
                        AttributeName = sortKey,
                        AttributeType = "N"
                    }
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = partitionKey,
                        KeyType = "HASH" //Partition key
                    },
                    new KeySchemaElement
                    {
                        AttributeName = sortKey,
                        KeyType = "RANGE" //Sort key
                    }
                },
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = readCapacityUnits,
                    WriteCapacityUnits = writeCapacityUnits
                },
                TableName = tableName
            };

            var response = await _dynamoDBClient.CreateTableAsync(request);
            return response.TableDescription;
        }

        private void CheckTableKeys(TableDescription status, string requiredPartitionKey, string requiredSortKey)
        {
            { // Check HASH and RANGE keys
                var partitionKey = status.KeySchema.FirstOrDefault(s => s.KeyType == "HASH");
                var sortKey = status.KeySchema.FirstOrDefault(s => s.KeyType == "RANGE");

                if (partitionKey == null || partitionKey.AttributeName != requiredPartitionKey)
                {
                    throw new InvalidOperationException(String.Format(
                        "DynamoDB table ${0} already exists but partitionKey does not match! Existing: ${1}. Required: ${2}.",
                        status.TableName, partitionKey, requiredPartitionKey
                    ));
                }
                if (sortKey == null || sortKey.AttributeName != requiredSortKey)
                {
                    throw new InvalidOperationException(String.Format(
                        "DynamoDB table ${0} already exists but sortKey does not match! Existing: ${1}. Required: ${2}.",
                        status.TableName, sortKey, requiredSortKey
                    ));
                }
            }

            { // Check HASH AND RANGE keys types
                var partitionKeyType = status.AttributeDefinitions.FirstOrDefault(a => a.AttributeName == requiredPartitionKey);
                var sortKeyType = status.AttributeDefinitions.FirstOrDefault(a => a.AttributeName == requiredSortKey);

                if (partitionKeyType == null || partitionKeyType.AttributeType != "S") {
                    throw new InvalidOperationException(String.Format(
                        "DynamoDB table ${0} already exists but partitionKey type is not 'S'!", status.TableName
                    ));
                }
                if (sortKeyType == null || sortKeyType.AttributeType != "N") {
                    throw new InvalidOperationException(String.Format(
                        "DynamoDB table ${0} already exists but sortKey type is not 'N'!", status.TableName
                    ));
                }
            }
        }
    }
}
