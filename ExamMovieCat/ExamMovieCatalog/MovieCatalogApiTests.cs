using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RestSharp;
using RestSharp.Authenticators;
using ExamMovieCatalog.Models;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace ExamMovieCatalog
{
    [TestFixture]
    public class Tests
    {

        private RestClient client;
        private static string lastCreatedMovieId;

        private const string BaseUrl = "http://144.91.123.158:5000";
        private const string StaticToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJKd3RTZXJ2aWNlQWNjZXNzVG9rZW4iLCJqdGkiOiIwMTM1NmI1ZC1kMDg5LTRlMTAtYWJlZS0yNzBjZGNiNmI3ZjAiLCJpYXQiOiIwNC8xOC8yMDI2IDA2OjQ3OjE0IiwiVXNlcklkIjoiMTdjODJjNjAtMmJhMi00ZmQ4LTYyN2QtMDhkZTc2OTcxYWI5IiwiRW1haWwiOiJpbGtvdHJhcG92QGV4YW1wbGUuY29tIiwiVXNlck5hbWUiOiJpbGtvdHJhcG92IiwiZXhwIjoxNzc2NTE2NDM0LCJpc3MiOiJNb3ZpZUNhdGFsb2dfQXBwX1NvZnRVbmkiLCJhdWQiOiJNb3ZpZUNhdGFsb2dfV2ViQVBJX1NvZnRVbmkifQ.rytSLc2yKQE-il5fzYXpopPa0F_Zh_V9XsI7vwIubs4";

        private const string LoginEmail = "ilkotrapov@example.com";
        private const string LoginPassword = "ilkotrapov";

        [OneTimeSetUp]
        public void Setup()
        {
            string jwtToken;
            
            if (!string.IsNullOrWhiteSpace(StaticToken))
            {
                jwtToken = StaticToken;
            }
            else
            {
                jwtToken = GetJwtToken(LoginEmail, LoginPassword);
            }

            var options = new RestClientOptions(BaseUrl)
            {
                Authenticator = new JwtAuthenticator(jwtToken)
            };

            this.client = new RestClient(options);

        }

        private string GetJwtToken(string email, string password)
        {
            var tempClient = new RestClient(BaseUrl);
            var request = new RestRequest("/api/User/Authentication", Method.Post);
            request.AddJsonBody(new { email, password });

            var response = tempClient.Execute(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = JsonSerializer.Deserialize<JsonElement>(response.Content);
                var token = content.GetProperty("accessToken").GetString();

                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException("Token is not found in the response.");
                }
                return token;
            }
            else
            {
                throw new InvalidOperationException($"Failed to authenticate. Status code: {response.StatusCode}, Response: {response.Content}");
            }
        }

        [Order(1)]
        [Test]
        public void CreateNewMovie_WithRequiredFields_ShouldReturnSuccess()
        {
            var movieData = new MovieDTO
            {
                Title = "Test Idea",
                Description = "This is a test idea description.",
                
            };

            var request = new RestRequest("/api/Movie/Create", Method.Post);
            request.AddJsonBody(movieData);

            var response = this.client.Execute(request);

            var createResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Expected status code 200 OK.");
            Assert.IsNotNull(createResponse.Movie, "Expected a movie object in the response.");
            Assert.IsFalse(string.IsNullOrEmpty(createResponse.Movie.Id), "Expected the created movie to have a non-empty ID.");
            Assert.That(createResponse.Msg, Is.EqualTo("Movie created successfully!"));
            
            lastCreatedMovieId = createResponse?.Movie?.Id;
            if (string.IsNullOrWhiteSpace(lastCreatedMovieId))
            {
                var allReq = new RestRequest("/api/Movie/All", Method.Get);
                var allResp = this.client.Execute(allReq);
                var responseItems = JsonSerializer.Deserialize<List<ApiResponseDTO>>(allResp.Content);
                lastCreatedMovieId = responseItems?.LastOrDefault()?.Movie?.Id;
            }

        }

        [Order(2)]
        [Test]
        public void EditTheLastCreatedMovie_ShouldReturnSuccess()
        {
             if (string.IsNullOrWhiteSpace(lastCreatedMovieId))
            {
                var createMovie = new MovieDTO
                {
                    Title = "Temp Movie for Edit",
                    Description = "Temporary movie created for edit test."
                };

                var createRequest = new RestRequest("/api/Movie/Create", Method.Post);
                createRequest.AddJsonBody(createMovie);
                var createResponse = this.client.Execute(createRequest);
                Assert.That(createResponse, Is.Not.Null, "Create request returned null response.");
                Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Create request failed: " + (createResponse?.Content ?? "<no content>"));
                var createResponseDto = JsonSerializer.Deserialize<ApiResponseDTO>(createResponse.Content);
                lastCreatedMovieId = createResponseDto?.Movie?.Id;
                if (string.IsNullOrWhiteSpace(lastCreatedMovieId))
                {
                    var allReq = new RestRequest("/api/Movie/All", Method.Get);
                    var allResp = this.client.Execute(allReq);
                    var responseItems = JsonSerializer.Deserialize<List<ApiResponseDTO>>(allResp.Content);
                    lastCreatedMovieId = responseItems?.LastOrDefault()?.Movie?.Id;
                }
                Assert.That(lastCreatedMovieId, Is.Not.Null.And.Not.Empty, "Failed to obtain movie ID for edit test.");
            }

            var editRequestData = new MovieDTO
            {
                Title = "Updated Test Movie",
                Description = "This is an updated test movie description."
            };
            var request = new RestRequest("/api/Movie/Edit", Method.Put);

            request.AddQueryParameter("movieId", lastCreatedMovieId);
            request.AddJsonBody(editRequestData);

            var response = this.client.Execute(request);
            var editResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Expected status code 200 OK.");
            Assert.That(editResponse.Msg, Is.EqualTo("Movie edited successfully!"));
            

        }

        [Order(3)]
        [Test]
        public void GetAllMovies_ShouldReturnSuccess()
        {
            var request = new RestRequest("/api/Catalog/All", Method.Get);
            var response = this.client.Execute(request);

            var responseItems = JsonSerializer.Deserialize<List<ApiResponseDTO>>(response.Content);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Expected status code 200 OK.");
            Assert.That(responseItems, Is.Not.Empty);
            Assert.That(responseItems, Is.Not.Null);

        }



        [Order(4)]
        [Test]

        public void DeleteTheLastCreatedMovie_ShouldReturnSuccess()
        {
            var request = new RestRequest("/api/Movie/Delete", Method.Delete);
            request.AddQueryParameter("movieId", lastCreatedMovieId);
            var response = this.client.Execute(request);

            var deleteResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(deleteResponse.Msg, Is.EqualTo("Movie deleted successfully!"));
        }



        [Order(5)]
        [Test]
        public void TryMovieCreation_WithoutRequiredFields_ShouldReturnBadRequest()
        {
            var movieData = new MovieDTO
            {
                Title = "",
                Description = ""
            };
            var request = new RestRequest("/api/Movie/Create", Method.Post);
            request.AddJsonBody(movieData);

            var response = this.client.Execute(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Expected status code 400 Bad Request.");
        }

        [Order(6)]
        [Test]
        public void EditNonExistingMovie_ShouldReturnBadRequest()
        {
            string invalidMovieId = "99999999";
            var editRequestData = new MovieDTO
            {
                Title = "Updated Test Movie with Invalid ID",
                Description = "This is an updated test movie description with invalid ID."
            };
            var request = new RestRequest("/api/Movie/Edit", Method.Put);
            request.AddQueryParameter("movieId", invalidMovieId);
            request.AddJsonBody(editRequestData);

            var response = this.client.Execute(request);

            
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Expected status code 400 Bad Request.");
            var errorResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);

            Assert.That(errorResponse.Msg, Is.EqualTo("Unable to edit the movie! Check the movieId parameter or user verification!"));
        }

        [Order(7)]
        [Test]
        public void DeleteNonExistingMovie_ShouldReturnBadRequest()
        {
            string invalidMovieId = "99999999";
            var request = new RestRequest("/api/Movie/Delete", Method.Delete);
            request.AddQueryParameter("movieId", invalidMovieId);

            var response = this.client.Execute(request);

            var errorResponse = JsonSerializer.Deserialize<ApiResponseDTO>(response.Content);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "Expected status code 400 Bad Request.");
            Assert.That(errorResponse.Msg, Is.EqualTo("Unable to delete the movie! Check the movieId parameter or user verification!"));
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            this.client?.Dispose();
        }
    }
}