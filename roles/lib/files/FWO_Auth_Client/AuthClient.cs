﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Linq;

namespace FWO.Auth.Client
{
    public class AuthClient
    {
        private readonly HttpClient HttpClient;
        private readonly string AuthServerUri;

        public AuthClient(string AuthServerUri)
        {
            this.AuthServerUri = AuthServerUri;
            HttpClient = new HttpClient();
        }

        public async Task<string> GetJWT(string Username, string Password)
        {
            Dictionary<string, string> Parameters = new Dictionary<string, string>
            {
                { "Username", Username },
                { "Password", Password }
            };

            string ParametersJson = JsonSerializer.Serialize(Parameters);

            StringContent content = new StringContent(ParametersJson);           
            Console.WriteLine("Sending GetJWT Request...");
            HttpResponseMessage response = await HttpClient.PostAsync(AuthServerUri + "jwt", content);
            return await response.Content.ReadAsStringAsync();
        }

        public static RsaSecurityKey ExtractKeyFromPem(string RawKey, bool isPrivateKey)
        {
            bool isRsaKey = true;
            string keyText = ExtractKeyFromPemAsString(RawKey, isPrivateKey,  out isRsaKey);
            RsaSecurityKey rsaKey = null;
           
            try
            {
                byte[] keyBytes = Convert.FromBase64String(keyText);
               // creating the RSA key 
                RSACryptoServiceProvider provider = new RSACryptoServiceProvider();
                if (isPrivateKey)
                {   
                    if (isRsaKey)
                    {   // ubuntu 20.04:
                        provider.ImportRSAPrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
                    } else
                    {   // debian 10:
                        provider.ImportPkcs8PrivateKey(new ReadOnlySpan<byte>(keyBytes), out _);
                    }
                }
                else   // public key
                    provider.ImportSubjectPublicKeyInfo(new ReadOnlySpan<byte>(keyBytes), out _);
                rsaKey = new RsaSecurityKey(provider);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
            }
            return rsaKey;
        }
        public static string ExtractKeyFromPemAsString(string rawKey, bool isPrivateKey, out bool isRsaKey)
        {
            string keyText = null;
            isRsaKey = true;
            Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString rawKey={rawKey}");
            try
            {
                // removing armor of PEM file (first and last line)
                List<string> lines = new List<string>(rawKey.Split('\n'));
                var firstline = lines.First();
                if (firstline.Contains("RSA"))
                {
                    isRsaKey = true;
                    // Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString: firstline={firstline}, contains rsa = true");
                }
                else
                {
                    isRsaKey = false;
                    // Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString: firstline={firstline}, contains rsa = false");
                }
                keyText = String.Join('\n', lines.GetRange(1,lines.Count-2).ToArray());
                keyText = keyText.Replace("\n", "");    // remove line breaks
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(new System.Diagnostics.StackTrace().ToString());
            }
            Console.WriteLine($"AuthClient::ExtractKeyFromPemAsString keyText={keyText}");
            return keyText;
        }
    }
}

