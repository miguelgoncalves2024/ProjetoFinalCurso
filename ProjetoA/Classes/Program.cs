﻿using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Windows.System;
using Windows.UI;

namespace MongoDBInjectionExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // Conectar ao banco de dados MongoDB
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("minhaDatabase");
            var collection = database.GetCollection<BsonDocument>("minhaColecao");

            // Obter entrada do usuário
            Console.WriteLine("Digite o nome de usuário para buscar:");
            string nomeUsuario = Console.ReadLine();
             
            // Criar filtro de pesquisa com injeção de código
            var filtro = BsonDocument.Parse($@"{{ nomeUsuario: {{ $regex: /{nomeUsuario}/i }} }}"); 

            // Buscar documentos com base no filtro vulnerável 
            var documentos = collection.Find(filtro).ToList();        
             
            // Exibir os documentos encontrados
            foreach (var documento in documentos)
            {
                Console.WriteLine(documento.ToJson());
            }
        } 

        public async Task<IActionResult> GetUser(string id)     
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out var validId))
            {
                return BadRequest("Invalid user id");
            }

            var filter = Builders<User>.Filter.Eq("Id", validId);   
            var user = await _context.Users.Find(filter).FirstOrDefaultAsync(); 

            return Ok(user);
        }
    } 
}