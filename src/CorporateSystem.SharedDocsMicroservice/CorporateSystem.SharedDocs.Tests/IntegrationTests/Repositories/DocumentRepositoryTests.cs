﻿using CorporateSystem.SharedDocs.Infrastructure.Dtos;
using CorporateSystem.SharedDocs.Infrastructure.Migrations;
using CorporateSystem.SharedDocs.Infrastructure.Options;
using CorporateSystem.SharedDocs.Infrastructure.Repositories.Filters;
using CorporateSystem.SharedDocs.Infrastructure.Repositories.Implementations;
using CorporateSystem.SharedDocs.Infrastructure.Repositories.Interfaces;
using CorporateSystem.SharedDocs.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace CorporateSystem.SharedDocs.Tests.IntegrationTests.Repositories;

[Collection("PostgresCollection")]
public class DocumentRepositoryTests
{
    private readonly PostgresContainer _fixture;

    public DocumentRepositoryTests(PostgresContainer postgresContainer)
    {
        _fixture = postgresContainer;
        
        var migrator = new Migrator(_fixture.ConnectionString);
        migrator.ApplyMigrations();
    }

    [Fact]
    public async Task GetAsync_ReturnsDocument_WhenIdExists()
    {
        // Arrange
        var repository = GetRepository();

        var title = StringHelper.GetUniqueString();
        var content = StringHelper.GetUniqueString();
        
        var createDocumentDto = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = title,
            Content = content
        };
        
        var createdIds = await repository.CreateAsync([createDocumentDto]);
        var documentId = createdIds[0];

        // Act
        var result = await repository.GetAsync(documentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title, result.Title);
        Assert.Equal(content, result.Content);
    }

    [Fact]
    public async Task CreateAsync_AddsDocumentsToDatabase()
    {
        // Arrange
        var repository = GetRepository();

        var title1 = StringHelper.GetUniqueString();
        var title2 = StringHelper.GetUniqueString();
        var content1 = StringHelper.GetUniqueString();
        var content2 = StringHelper.GetUniqueString();
        
        var createDocumentDto1 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = title1,
            Content = content1
        };
        
        var createDocumentDto2 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = title2,
            Content = content2
        };

        // Act
        var ids = await repository.CreateAsync([createDocumentDto1, createDocumentDto2]);

        // Assert
        Assert.Equal(2, ids.Length);

        var document1 = await repository.GetAsync(ids[0]);
        Assert.NotNull(document1);
        Assert.Equal(title1, document1.Title);
        Assert.Equal(content1, document1.Content);

        var document2 = await repository.GetAsync(ids[1]);
        Assert.NotNull(document2);
        Assert.Equal(title2, document2.Title);
        Assert.Equal(content2, document2.Content);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDocumentInDatabase()
    {
        // Arrange
        var repository = GetRepository();

        var ownerId = Int.GetUniqueNumber();

        var oldTitle = StringHelper.GetUniqueString();
        var oldContent = StringHelper.GetUniqueString();
        
        var createDocumentDto = new CreateDocumentDto
        {
            OwnerId = ownerId,
            Title = oldTitle,
            Content = oldContent
        };
        
        var createdIds = await repository.CreateAsync([createDocumentDto]);
        var documentId = createdIds[0];

        var updatedTitle = StringHelper.GetUniqueString();
        var updatedContent = StringHelper.GetUniqueString();
        
        var updateDocumentDto = new UpdateDocumentDto
        {
            OwnerId = ownerId,
            Title = updatedTitle,
            Content = updatedContent
        };

        // Act
        await repository.UpdateAsync(documentId, updateDocumentDto);

        // Assert
        var updatedDocument = await repository.GetAsync(documentId);
        Assert.NotNull(updatedDocument);
        Assert.Equal(updatedTitle, updatedDocument.Title);
        Assert.Equal(updatedContent, updatedDocument.Content);
    }

    [Fact]
    public async Task DeleteAsync_RemovesDocumentsFromDatabase()
    {
        // Arrange
        var repository = GetRepository();
        
        var createDocumentDto = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = StringHelper.GetUniqueString(),
            Content = StringHelper.GetUniqueString()
        };
        var createdIds = await repository.CreateAsync([createDocumentDto]);
        var documentId = createdIds[0];

        // Act
        await repository.DeleteAsync([documentId]);

        // Assert
        var deletedDocument = await repository.GetAsync(documentId);
        Assert.Null(deletedDocument);
    }
    
    [Fact]
    public async Task GetAsync_WithIdsFilter_ReturnsFilteredDocuments()
    {
        // Arrange
        var repository = GetRepository();

        var title1 = StringHelper.GetUniqueString();
        var content1 = StringHelper.GetUniqueString();
        var title2 = StringHelper.GetUniqueString();
        var content2 = StringHelper.GetUniqueString();
        
        var createDocumentDto1 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = title1,
            Content = content1
        };
        var createDocumentDto2 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = title2,
            Content = content2
        };

        var createdIds = await repository.CreateAsync([createDocumentDto1, createDocumentDto2]);
        var documentId1 = createdIds[0];

        var filter = new DocumentFilter
        {
            Ids = [documentId1]
        };

        // Act
        var result = (await repository.GetAsync(filter)).ToArray();

        // Assert
        Assert.Single(result);
        var document = result.First();
        Assert.Equal(documentId1, document.Id);
        Assert.Equal(title1, document.Title);
    }
    
    [Fact]
    public async Task GetAsync_WithContentsFilter_ReturnsFilteredDocuments()
    {
        // Arrange
        var repository = GetRepository();

        var title2 = StringHelper.GetUniqueString();
        var content2 = StringHelper.GetUniqueString();
        
        var createDocumentDto1 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = StringHelper.GetUniqueString(),
            Content = StringHelper.GetUniqueString()
        };
        var createDocumentDto2 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = title2,
            Content = content2
        };

        await repository.CreateAsync([createDocumentDto1, createDocumentDto2]);

        var filter = new DocumentFilter
        {
            Contents = [content2]
        };

        // Act
        var result = (await repository.GetAsync(filter)).ToArray();

        // Assert
        Assert.Single(result);
        var document = result.First();
        Assert.Equal(title2, document.Title);
        Assert.Equal(content2, document.Content);
    }
    
    [Fact]
    public async Task GetAsync_WithTitlesFilter_ReturnsFilteredDocuments()
    {
        // Arrange
        var repository = GetRepository();

        var title2 = StringHelper.GetUniqueString();
        var content2 = StringHelper.GetUniqueString();
        
        var createDocumentDto1 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = StringHelper.GetUniqueString(),
            Content = StringHelper.GetUniqueString()
        };
        var createDocumentDto2 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = title2,
            Content = content2
        };

        await repository.CreateAsync([createDocumentDto1, createDocumentDto2]);

        var filter = new DocumentFilter
        {
            Titles = [title2]
        };

        // Act
        var result = (await repository.GetAsync(filter)).ToArray();

        // Assert
        Assert.Single(result);
        var document = result.First();
        Assert.Equal(title2, document.Title);
        Assert.Equal(content2, document.Content);
    }
    
    [Fact]
    public async Task GetAsync_WithOwnerIdsFilter_ReturnsFilteredDocuments()
    {
        // Arrange
        var repository = GetRepository();

        var ownerId1 = Int.GetUniqueNumber();
        var title1 = StringHelper.GetUniqueString();
        
        var createDocumentDto1 = new CreateDocumentDto
        {
            OwnerId = ownerId1,
            Title = title1,
            Content = StringHelper.GetUniqueString()
        };
        var createDocumentDto2 = new CreateDocumentDto
        {
            OwnerId = Int.GetUniqueNumber(),
            Title = StringHelper.GetUniqueString(),
            Content = StringHelper.GetUniqueString()
        };

        await repository.CreateAsync([createDocumentDto1, createDocumentDto2]);

        var filter = new DocumentFilter
        {
            OwnerIds = [ownerId1]
        };

        // Act
        var result = (await repository.GetAsync(filter)).ToArray();

        // Assert
        Assert.Single(result);
        var document = result.First();
        Assert.Equal(ownerId1, document.OwnerId);
        Assert.Equal(title1, document.Title);
    }
    
    private IDocumentRepository GetRepository() => 
        new DocumentRepository(Options.Create(new PostgresOptions
        {
            ConnectionString = _fixture.ConnectionString
        }));
}