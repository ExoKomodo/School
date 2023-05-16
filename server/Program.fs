open Constants
open Giraffe
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.Extensions.DependencyInjection
open Models
open MongoDB.Driver
open System
open System.Net.Http
open System.Text.Json.Serialization

// NOTE: Initialize Mongo

let initializeMongo () =
  let connectionString = Environment.GetEnvironmentVariable("MONGODB_URI")
  match connectionString with
  | null ->
    printfn "You must set your 'MONGODB_URI' environmental variable. See\n\t https://www.mongodb.com/docs/drivers/go/current/usage-examples/#environment-variable"
    exit 1
  | _ ->
    let client = new MongoClient(connectionString)
    client.GetDatabase("admin")

let database = initializeMongo()
let courseCollection = database.GetCollection<Course>("courses")

let webApp =
  (choose
  [
    GET >=>
      routex "(/?)" >=> Index.get
    subRoute "/api" (choose
      [
        GET >=>
          routex "(/?)" >=> Api.Index.get
        subRoute "/v1" (choose
          [
            GET >=> (choose
              [
                routex "(/?)" >=> Api.V1.Index.get
                routex  "/blog(/?)" >=> Api.V1.Blog.getAll
                routef  "/blog/%s" Api.V1.Blog.get
                Helpers.mustBeLoggedIn >=> (choose
                  [
                    routex  "/course(/?)" >=> Api.V1.Course.getAllMetadata courseCollection
                    routef  "/course/%s" (Api.V1.Course.get courseCollection)
                    routex  "/user/info(/?)" >=> Api.V1.User.getInfo
                  ]
                )
              ]
            )
            DELETE
            >=> routef  "/course/%s" (Api.V1.Course.delete courseCollection)
            POST
            >=> routex "/course(/?)"
            >=> bindJson<Course> (fun course -> Api.V1.Course.post courseCollection course)
            PUT
            >=> routex "/course(/?)"
            >=> bindJson<Course> (fun course -> Api.V1.Course.put courseCollection course)
          ]
        )
      ]
    )
  ]
)

let configureCors (builder : CorsPolicyBuilder) =
  builder
    .WithOrigins(
      // NOTE: Development client
      "http://localhost:5173",
      "http://127.0.0.1:5173",
      // NOTE: Development server
      "http://localhost:5000",
      // NOTE: Production client
      "https://edu.exokomodo.com",
      // NOTE: Production server
      "https://services.edu.exokomodo.com"
    )
    .AllowAnyMethod()
    .AllowAnyHeader() |> ignore

let configureServices (services : IServiceCollection) =
  services
    .AddAuthentication(
      fun options ->
        options.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
        options.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(
      fun options ->
        options.Authority <- $"https://exokomodo.us.auth0.com/"
        options.Audience <- "https://services.edu.exokomodo.com"
    )
  |> ignore
  services
    .AddCors()
    .AddGiraffe()
  |> ignore

  let serializationOptions = SystemTextJson.Serializer.DefaultOptions
  serializationOptions.Converters.Add(JsonFSharpConverter(JsonUnionEncoding.FSharpLuLike))
  services.AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(serializationOptions)) |> ignore

let builder = WebApplication.CreateBuilder()

configureServices builder.Services

let app = builder.Build()
// NOTE: Order matters. CORS must be configured before starting Giraffe.
app.UseAuthentication() |> ignore
app.UseCors configureCors |> ignore
app.UseGiraffe webApp
app.Run()

type Program() = class end
