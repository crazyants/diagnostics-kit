﻿/**
 *  Part of the Diagnostics Kit
 *
 *  Copyright (C) 2016  Sebastian Solnica
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.

 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 */

using Elasticsearch.Net;
using LowLevelDesign.Diagnostics.LogStore.ElasticSearch.Models;
using Nest;
using System;
using System.Configuration;
using System.Linq;


namespace LowLevelDesign.Diagnostics.LogStore.ElasticSearch
{
    internal class ElasticSearchClientConfiguration
    {
        public const string MainConfigIndex = "lldconf";

        private static readonly bool isCluster;
        private static readonly Uri[] elasticUris;
        private static readonly bool requiresAuthentication;
        private static readonly string username;
        private static readonly string passwd;
        private static readonly bool isProxied;
        private static readonly Uri proxyUri;
        private static readonly string proxyUsername;
        private static readonly string proxyPassword;
        private static int shardsNum;
        private static int replicas;

        static ElasticSearchClientConfiguration()
        {
            var elasticConfigSection = ConfigurationManager.GetSection("elasticLogStore") as ElasticSearchLogStoreConfigSection;
            if (elasticConfigSection == null) {
                throw new ConfigurationErrorsException("elasticLogStore section is required in the application configuration file.");
            }
            var urls = elasticConfigSection.ElasticUrl;
            if (string.IsNullOrEmpty(urls)) {
                throw new ConfigurationErrorsException("elasticUrl must be provided - otherwise how we can connect to the ES cluster?");
            }
            var elasticUrls = urls.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (elasticUrls.Length == 0) {
                throw new ConfigurationErrorsException("elasticUrl must be provided - otherwise how we can connect to the ES cluster? (ERR2)");
            }
            isCluster = elasticUrls.Length > 0;
            elasticUris = elasticUrls.Select(u => new Uri(u)).ToArray();

            username = elasticConfigSection.UserName;
            passwd = elasticConfigSection.Password;
            requiresAuthentication = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(passwd);

            shardsNum = elasticConfigSection.Shards;
            replicas = elasticConfigSection.Replicas;

            isProxied = !string.IsNullOrEmpty(elasticConfigSection.ProxyUrl);
            if (isProxied) {
                proxyUri = new Uri(elasticConfigSection.ProxyUrl);
                proxyUsername = elasticConfigSection.ProxyUsername;
                proxyPassword = elasticConfigSection.ProxyPassword;
            }

            // make sure main config index exists
            // check if the application status index exists
            var eclient = CreateClient(MainConfigIndex);
            var resp = eclient.IndexExists(MainConfigIndex);
            if (resp.IsValid && !resp.Exists)
            {
                eclient.CreateIndex(MainConfigIndex, c => c.Mappings(map => map.Map<ElasticUser>(
                        m => m.AutoMap()).Map<ElasticApplication>(
                        m => m.AutoMap()).Map<ElasticApplicationConfig>(
                        m => m.AutoMap()).Map<ElasticApplicationStatus>(
                        m => m.AutoMap()))
                    .Settings(s => s.NumberOfShards(ShardsNum)
                        .NumberOfReplicas(ReplicasNum)));
            }
        }

        public static ElasticClient CreateClient(string indexName)
        {
            ConnectionSettings settings;
            if (!isCluster) {
                settings = new ConnectionSettings(elasticUris[0]);
            } else {
                settings = new ConnectionSettings(new SniffingConnectionPool(elasticUris));
            }
            settings = settings.DefaultIndex(indexName);
            if (isProxied) {
                settings = settings.Proxy(proxyUri, proxyUsername, proxyPassword);
            }
            settings = settings.ThrowExceptions(false).PluralizeTypeNames();
            if (requiresAuthentication)
            {
                settings.BasicAuthentication(username, passwd);
            }
            return new ElasticClient(settings);
        }

        public static int ShardsNum { get { return shardsNum; } }

        public static int ReplicasNum { get { return replicas; } }
    }

    public sealed class ElasticSearchLogStoreConfigSection : ConfigurationSection
    {
        [ConfigurationProperty("elasticUrl", IsRequired = true)]
        public string ElasticUrl { get { return (string)this["elasticUrl"]; } }

        [ConfigurationProperty("username", IsRequired = false)]
        public string UserName { get { return (string)this["username"]; } }

        [ConfigurationProperty("passwd", IsRequired = false)]
        public string Password { get { return (string)this["passwd"]; } }

        [ConfigurationProperty("shards", IsRequired = false, DefaultValue = 5)]
        public int Shards { get { return (int)this["shards"]; } }

        [ConfigurationProperty("replicas", IsRequired = false, DefaultValue = 0)]
        public int Replicas { get { return (int)this["replicas"]; } }

        [ConfigurationProperty("proxyUrl", IsRequired = false)]
        public string ProxyUrl { get { return (string)this["proxyUrl"]; } }

        [ConfigurationProperty("proxyUsername", IsRequired = false)]
        public string ProxyUsername { get { return (string)this["proxyUsername"]; } }

        [ConfigurationProperty("proxyPassword", IsRequired = false)]
        public string ProxyPassword { get { return (string)this["proxyPassword"]; } }
    }
}
