﻿using CorporateSystem.SharedDocs.Domain.Entities;
using CorporateSystem.SharedDocs.Domain.Enums;
using CorporateSystem.SharedDocs.Infrastructure.Dtos;
using CorporateSystem.SharedDocs.Infrastructure.Repositories.Filters;
using CorporateSystem.SharedDocs.Infrastructure.Repositories.Interfaces;
using CorporateSystem.SharedDocs.Services.Dtos;
using CorporateSystem.SharedDocs.Services.Exceptions;
using CorporateSystem.SharedDocs.Services.Services.Implementations;
using CorporateSystem.SharedDocs.Services.Services.Interfaces;
using CorporateSystem.SharedDocs.Tests.Builders;
using CorporateSystem.SharedDocs.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CreateDocumentDto = CorporateSystem.SharedDocs.Services.Dtos.CreateDocumentDto;

namespace CorporateSystem.SharedDocs.Tests.IntegrationTests.Services;

public class DocumentServiceTests
{
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly Mock<IDocumentRepository> _mockDocumentRepository;
    private readonly Mock<IDocumentUserRepository> _mockDocumentUserRepository;
    private readonly Mock<IAuthApiService> _mockAuthApiService;
    private readonly Mock<IDocumentCompositeRepository> _mockDocumentCompositeRepository;
    private readonly Mock<IDocumentChangeLogService> _mockDocumentChangeLogService;
    private readonly Mock<IBanWordsService> _mockBanWordsService;
    private readonly DocumentService _documentService;

    public DocumentServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocumentService>>();
        _mockDocumentRepository = new Mock<IDocumentRepository>();
        _mockDocumentUserRepository = new Mock<IDocumentUserRepository>();
        _mockAuthApiService = new Mock<IAuthApiService>();
        _mockDocumentCompositeRepository = new Mock<IDocumentCompositeRepository>();
        _mockDocumentChangeLogService = new Mock<IDocumentChangeLogService>();
        _mockBanWordsService = new Mock<IBanWordsService>();
        
        _documentService = new DocumentService(
            _mockLogger.Object,
            _mockDocumentRepository.Object,
            _mockDocumentUserRepository.Object,
            _mockAuthApiService.Object,
            _mockDocumentCompositeRepository.Object,
            _mockDocumentChangeLogService.Object,
            _mockBanWordsService.Object);
    }
    
    [Fact]
    public async Task CreateDocumentAsync_ReturnsDocumentId_WhenCreatedSuccessfully()
    {
        // Arrange
        var dto = new CreateDocumentDto
        {
            OwnerId = 1,
            Title = "Test Document",
            Content = "Test Content"
        };

        _mockDocumentRepository
            .Setup(repo => repo.CreateAsync(
                It.IsAny<Infrastructure.Dtos.CreateDocumentDto[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);
        
        // Act
        var result = await _documentService.CreateDocumentAsync(dto);

        // Assert
        result.Should().Be(1);
        
        _mockDocumentRepository.Verify(repo => repo.CreateAsync(
            It.Is<Infrastructure.Dtos.CreateDocumentDto[]>(dtos =>
                dtos.Length == 1 &&
                dtos[0].OwnerId == dto.OwnerId &&
                dtos[0].Title == dto.Title &&
                dtos[0].Content == dto.Content),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task AddUsersToDocumentAsync_AddsUsers_WhenUsersAreNotAlreadyAdded()
    {
        // Arrange
        var dto = new AddUserToDocumentDto
        {
            DocumentId = 1,
            UserInfos =
            [
                new DocumentUserInfo { UserId = 1, AccessLevel = AccessLevel.Reader },
                new DocumentUserInfo { UserId = 2, AccessLevel = AccessLevel.Writer }
            ]
        };

        _mockDocumentRepository
            .Setup(repo => repo.GetAsync(dto.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DocumentBuilder()
                    .WithId(dto.DocumentId)
                    .Build());

        _mockDocumentUserRepository
            .Setup(repo => repo.GetAsync(
                It.IsAny<DocumentUserFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _documentService.AddUsersToDocumentAsync(dto);

        // Assert
        _mockDocumentUserRepository.Verify(repo => repo.CreateAsync(
            It.Is<CreateDocumentUserDto[]>(users =>
                users.Length == 2 &&
                users.Any(u => u.UserId == 1 && u.AccessLevel == AccessLevel.Reader) &&
                users.Any(u => u.UserId == 2 && u.AccessLevel == AccessLevel.Writer)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task AddUsersToDocumentAsync_ThrowsException_WhenUserAlreadyAdded()
    {
        // Arrange
        var dto = new AddUserToDocumentDto
        {
            DocumentId = 1,
            UserInfos =
            [
                new DocumentUserInfo { UserId = 1, AccessLevel = AccessLevel.Reader }
            ]
        };

        _mockDocumentRepository
            .Setup(repo => repo.GetAsync(dto.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new DocumentBuilder()
                    .WithId(dto.DocumentId)
                    .Build());

        _mockDocumentUserRepository
            .Setup(repo => repo.GetAsync(
                It.IsAny<DocumentUserFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DocumentUser
                {
                    UserId = 1,
                    DocumentId = dto.DocumentId
                }
            ]);

        // Act & Assert
        var act = () => _documentService.AddUsersToDocumentAsync(dto);
        await act.Should().ThrowAsync<UserAlreadyExistException>()
            .WithMessage("Попытка добавить уже существующего пользователя");
    }
    
    [Fact]
    public async Task UpdateDocumentContentAsync_UpdatesContent_WhenUserHasWriterAccess()
    {
        // Arrange
        var dto = new UpdateDocumentContentDto
        {
            DocumentId = 1,
            UserId = 1,
            NewContent = "Updated Content"
        };

        _mockDocumentRepository
            .Setup(repo => repo.GetAsync(dto.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentBuilder()
                .WithId(dto.DocumentId)
                .Build());

        _mockDocumentUserRepository
            .Setup(repo => repo.GetAsync(
                It.IsAny<DocumentUserFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DocumentUser
                {
                    UserId = dto.UserId,
                    AccessLevel = AccessLevel.Writer
                }
            ]);

        // Act
        await _documentService.UpdateDocumentContentAsync(dto);

        // Assert
        _mockDocumentRepository.Verify(repo => repo.UpdateAsync(
            dto.DocumentId,
            It.IsAny<UpdateDocumentDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _mockDocumentChangeLogService.Verify(service => 
            service.AddChangeLogAsync(It.IsAny<ChangeLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task UpdateDocumentContentAsync_ThrowsException_WhenUserDoesNotHaveWriterAccess()
    {
        // Arrange
        var dto = new UpdateDocumentContentDto
        {
            DocumentId = 1,
            UserId = 1,
            NewContent = "Updated Content"
        };

        _mockDocumentRepository
            .Setup(repo => repo.GetAsync(dto.DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentBuilder()
                .WithId(dto.DocumentId)
                .Build());

        _mockDocumentUserRepository
            .Setup(repo => repo.GetAsync(
                It.IsAny<DocumentUserFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new DocumentUser
                {
                    UserId = dto.UserId,
                    AccessLevel = AccessLevel.Reader
                }
            ]);

        // Act & Assert
        Func<Task> act = () => _documentService.UpdateDocumentContentAsync(dto);
        await act.Should().ThrowAsync<InsufficientPermissionsException>()
            .WithMessage("У вас недостаточно прав для выполнения этой операции");
    }
    
    [Fact]
    public async Task GetDocumentUsersAsync_ReturnsUsers_WhenFilterMatches()
    {
        // Arrange
        var dto = new GetDocumentUsersDto
        {
            DocumentId = 1,
            UserIds = [2, 3]
        };

        var expectedUsers = new[]
        {
            new DocumentUser { UserId = 2, DocumentId = 1, AccessLevel = AccessLevel.Reader },
            new DocumentUser { UserId = 3, DocumentId = 1, AccessLevel = AccessLevel.Writer }
        };

        _mockDocumentUserRepository
            .Setup(repo => repo.GetAsync(
                It.Is<DocumentUserFilter>(filter =>
                    filter.DocumentIds.Contains(dto.DocumentId) &&
                    filter.UserIds.SequenceEqual(dto.UserIds)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsers);

        // Act
        var result = await _documentService.GetDocumentUsersAsync(dto);

        // Assert
        result.Should().BeEquivalentTo(expectedUsers);
    }
    
    [Fact]
    public async Task GetDocumentUsersAsync_ReturnsEmptyList_WhenNoUsersMatchFilter()
    {
        // Arrange
        var dto = new GetDocumentUsersDto
        {
            DocumentId = 1,
            UserIds = [2, 3]
        };

        _mockDocumentUserRepository
            .Setup(repo => repo.GetAsync(
                It.Is<DocumentUserFilter>(filter =>
                    filter.DocumentIds.Contains(dto.DocumentId) &&
                    filter.UserIds.SequenceEqual(dto.UserIds)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _documentService.GetDocumentUsersAsync(dto);

        // Assert
        result.Should().BeEmpty();
    }
    
    [Fact]
    public async Task DeleteDocumentAsync_DeletesDocuments_WhenIdsProvided()
    {
        // Arrange
        var documentIds = new[] { 1, 2 };

        // Act
        await _documentService.DeleteDocumentAsync(new DeleteDocumentDto(Ids: documentIds));

        // Assert
        _mockDocumentRepository.Verify(repo => repo.DeleteAsync(
            It.Is<DocumentFilter?>(dto => dto.Ids.SequenceEqual(documentIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task DeleteUsersFromCurrentDocumentAsync_DeletesUserFromDocument()
    {
        // Arrange

        var dto = new DeleteUserFromDocumentDto(1, 2);

        // Act
        await _documentService.DeleteUsersFromCurrentDocumentAsync(dto);

        // Assert
        _mockDocumentUserRepository.Verify(repo => repo.DeleteAsync(
            It.IsAny<DocumentUserFilter?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    
    [Fact]
    public async Task GetCurrentUserDocuments_ReturnsDocumentsForGivenUserId()
    {
        // Arrange
        var userId = 1;
        var expectedDocuments = new[]
        {
            new DocumentInfo
            {
                Title = StringHelper.GetUniqueString(),
                Id = Int.GetUniqueNumber()
            },
            new DocumentInfo
            {
                Title = StringHelper.GetUniqueString(),
                Id = Int.GetUniqueNumber()
            }
        };
        
        _mockDocumentCompositeRepository
            .Setup(repo => repo.GetAsync(
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDocuments);

        // Act
        var result = (await _documentService.GetCurrentUserDocuments(userId)).ToArray();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public async Task GetCurrentUserDocuments_ReturnsEmptyList_WhenNoDocumentsFound()
    {
        var notExistingUserId = 999;
        
        _mockDocumentRepository
            .Setup(repo => repo.GetAsync(
                It.IsAny<DocumentFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _documentService.GetCurrentUserDocuments(notExistingUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDocumentAsync_ThrowException_WhenDocumentIsNull()
    {
        // Arrange
        var documentId = 1;

        _mockDocumentRepository
            .Setup(repo =>
                repo.GetAsync(
                    It.Is<int>(value => value == documentId), 
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => null);
        
        // Act
        var result = () => _documentService.GetDocumentAsync(documentId);

        // Assert
        await result.Should().ThrowAsync<FileNotFoundException>().WithMessage("Документ не был найден");
    }
    
    [Fact]
    public async Task GetDocumentAsync_ReturnsDocument()
    {
        // Arrange
        var documentId = 1;

        _mockDocumentRepository
            .Setup(repo =>
                repo.GetAsync(
                    It.Is<int>(value => value == documentId), 
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentBuilder()
                .WithId(documentId)
                .Build());
        
        // Act
        var result = await _documentService.GetDocumentAsync(documentId);

        // Assert
        result.Should().NotBeNull();
    }
}