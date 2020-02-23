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
    public static class DynamoDBExtensions
    {
        /// <summary>
        /// Checks if table for events with given names exists. If not it creates it.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when DynamoDB table already exists, but with different properties OR when table is being deleted.</exception>
        /// <exception cref="Amazon.DynamoDBv2.AmazonDynamoDBException">Thrown when timeout occurs when getting table status from AWS.</exception>
        public static Task CheckCreateEventsTable(
            this IAmazonDynamoDB dynamoDB, DynamoDBProviderOptions options,
            int initialReadCapacityUnits, int initialWriteCapacityUnits
        )
            => dynamoDB.CheckCreateTable(
                options.EventsTableName, options.EventsTableHashKey, options.EventsTableSortKey,
                initialReadCapacityUnits, initialWriteCapacityUnits
            );

        /// <summary>
        /// Checks if table for snapshots with given names exists. If not it creates it.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when DynamoDB table already exists, but with different properties OR when table is being deleted.</exception>
        /// <exception cref="Amazon.DynamoDBv2.AmazonDynamoDBException">Thrown when timeout occurs when getting table status from AWS.</exception>
        public static Task CheckCreateSnapshotsTable(
            this IAmazonDynamoDB dynamoDB, DynamoDBProviderOptions options,
            int initialReadCapacityUnits, int initialWriteCapacityUnits
        )
            => dynamoDB.CheckCreateTable(
                options.SnapshotsTableName, options.SnapshotsTableHashKey, options.SnapshotsTableSortKey,
                initialReadCapacityUnits, initialWriteCapacityUnits
            );

        private static async Task CheckCreateTable(
            this IAmazonDynamoDB dynamoDB,
            string tableName, string partitionKey, string sortKey, int readCapacityUnits, int writeCapacityUnits
        )
        {
            var existingTable = await dynamoDB.IsTableCreated(tableName, true);

            if (existingTable.Created)
            {
                CheckTableKeys(existingTable.TableDesc, partitionKey, sortKey);
            }
            else
            {
                var res = await dynamoDB.CreateTable(tableName, partitionKey, sortKey, readCapacityUnits, writeCapacityUnits);

                if (res.TableStatus != "ACTIVE")
                {
                    await Task.Delay(2000);
                    await dynamoDB.IsTableCreated(tableName, false);
                }
            }
        }

        private static async Task<(bool Created, TableDescription TableDesc)> IsTableCreated(
            this IAmazonDynamoDB dynamoDB, string tableName, bool falseAccepted
        )
        {
            var retry = 10;

            do
            {
                var (created, tableDesc, shouldRetry) = await TryCheckTable();

                if (!shouldRetry)
                    return (created, tableDesc);

                await Task.Delay(2000); // Wait 2 seconds.
            } while (retry-- > 0);

            // We've been waiting for 20s already. Lets throw exception.
            throw new AmazonDynamoDBException($"Failed to get status for DynamoDB table ${tableName}.");

            async Task<(bool, TableDescription, bool)> TryCheckTable()
            {
                try
                {
                    var res = await dynamoDB.DescribeTableAsync(
                        new DescribeTableRequest {TableName = tableName}
                    );

                    return res.Table.TableStatus.Value switch
                    {
                        "ACTIVE"   => (true, res.Table, false),
                        "DELETING" => throw new InvalidOperationException($"DynamoDB table ${tableName} is being deleted."),
                        _          => (false, null, true)
                    };
                }
                catch (ResourceNotFoundException)
                {
                    if (falseAccepted)
                    {
                        return (false, null, false);
                    }
                }

                return (false, null, true);
            }
        }

        private static async Task<TableDescription> CreateTable(
            this IAmazonDynamoDB dynamoDB,
            string tableName, string partitionKey, string sortKey, int readCapacityUnits, int writeCapacityUnits
        )
        {
            var request = new CreateTableRequest
            {
                AttributeDefinitions = new List<AttributeDefinition>
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

            var response = await dynamoDB.CreateTableAsync(request);

            return response.TableDescription;
        }

        private static void CheckTableKeys(TableDescription status, string requiredPartitionKey, string requiredSortKey)
        {
            // Check HASH and RANGE keys
            var partitionKey = status.KeySchema.FirstOrDefault(s => s.KeyType == "HASH");
            var sortKey = status.KeySchema.FirstOrDefault(s => s.KeyType == "RANGE");

            if (partitionKey == null || partitionKey.AttributeName != requiredPartitionKey)
            {
                throw new InvalidOperationException(
                    $"DynamoDB table ${status.TableName} already exists but partitionKey does not match! Existing: ${partitionKey}. Required: ${requiredPartitionKey}."
                );
            }

            if (sortKey == null || sortKey.AttributeName != requiredSortKey)
            {
                throw new InvalidOperationException(
                    $"DynamoDB table ${status.TableName} already exists but sortKey does not match! Existing: ${sortKey}. Required: ${requiredSortKey}."
                );
            }

            // Check HASH AND RANGE keys types
            var partitionKeyType = status.AttributeDefinitions.FirstOrDefault(a => a.AttributeName == requiredPartitionKey);
            var sortKeyType = status.AttributeDefinitions.FirstOrDefault(a => a.AttributeName == requiredSortKey);

            if (partitionKeyType == null || partitionKeyType.AttributeType != "S")
            {
                throw new InvalidOperationException(
                    $"DynamoDB table ${status.TableName} already exists but partitionKey type is not 'S'!"
                );
            }

            if (sortKeyType == null || sortKeyType.AttributeType != "N")
            {
                throw new InvalidOperationException(
                    $"DynamoDB table ${status.TableName} already exists but sortKey type is not 'N'!"
                );
            }
        }
    }
}