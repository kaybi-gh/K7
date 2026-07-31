using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RequestType = table.Column<string>(type: "text", nullable: false),
                    RequestData = table.Column<string>(type: "text", nullable: false),
                    TargetEntityType = table.Column<string>(type: "text", nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Lane = table.Column<int>(type: "integer", nullable: false),
                    WorkClass = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TriggeredBy = table.Column<int>(type: "integer", nullable: false),
                    FederationPeerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    ReclaimCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRetryAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    CancellationRequested = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContentRestrictionProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RuleFilter = table.Column<string>(type: "jsonb", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRestrictionProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceUniqueId = table.Column<string>(type: "text", nullable: true),
                    DeviceName = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<int>(type: "integer", nullable: false),
                    DeviceType = table.Column<int>(type: "integer", nullable: false),
                    OperatingSystem = table.Column<int>(type: "integer", nullable: false),
                    OperatingSystemVersion = table.Column<string>(type: "text", nullable: true),
                    DisplayHeight = table.Column<double>(type: "double precision", nullable: false),
                    DisplayWidth = table.Column<double>(type: "double precision", nullable: false),
                    NativeDeviceDetails_RawModel = table.Column<string>(type: "text", nullable: true),
                    NativeDeviceDetails_RawManufacturer = table.Column<string>(type: "text", nullable: true),
                    NativeDeviceDetails_RawName = table.Column<string>(type: "text", nullable: true),
                    NativeDeviceDetails_RawVersion = table.Column<string>(type: "text", nullable: true),
                    NativeDeviceDetails_RawPlatform = table.Column<string>(type: "text", nullable: true),
                    NativeDeviceDetails_RawIdiom = table.Column<string>(type: "text", nullable: true),
                    NativeDeviceDetails_RawDeviceType = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_Browser = table.Column<int>(type: "integer", nullable: true),
                    WebDeviceDetails_RawUserAgent = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawBrowserName = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawBrowserVersion = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawOperatingSystemName = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawOperatingSystemVersion = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawOperatingSystemVersionName = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawPlatformType = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawEngineName = table.Column<string>(type: "text", nullable: true),
                    WebDeviceDetails_RawEngineVersion = table.Column<string>(type: "text", nullable: true),
                    PlaybackCapabilities_SupportedMediaFormatIds = table.Column<string[]>(type: "text[]", nullable: false),
                    PlaybackCapabilities_SupportedSubtitlesCodecs = table.Column<string[]>(type: "text[]", nullable: false),
                    PlaybackCapabilities_SupportsHDR = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LibraryGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MediaType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CardColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetadataTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    NormalizedKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    PayloadFormat = table.Column<int>(type: "integer", nullable: false),
                    EventTypeNames = table.Column<string>(type: "text", nullable: false),
                    ProviderConfig = table.Column<string>(type: "text", nullable: false),
                    TitleTemplate = table.Column<string>(type: "text", nullable: true),
                    BodyTemplate = table.Column<string>(type: "text", nullable: true),
                    RawJsonTemplate = table.Column<string>(type: "text", nullable: true),
                    RuleFilter = table.Column<string>(type: "jsonb", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeerRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequesterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Token = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PeerServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OutboundClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OutboundClientSecret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InboundApplicationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AutoAddNewLibraries = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastTestSucceeded = table.Column<bool>(type: "boolean", nullable: true),
                    FederationAssertionSecret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PeeringToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharedProfileSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfileSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncPlayInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncPlayInvites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Libraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MediaType = table.Column<int>(type: "integer", nullable: false),
                    RootPath = table.Column<string>(type: "text", nullable: true),
                    MetadataProviderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MetadataLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "fr"),
                    MetadataFallbackLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                    MetadataRefreshIntervalDays = table.Column<int>(type: "integer", nullable: true),
                    RootPathAccessible = table.Column<bool>(type: "boolean", nullable: true),
                    IntroDetectionEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ThemeSongGenerationEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SeekbarThumbnailGenerationEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ChapterExtractionEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MusicAudioAnalysisEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TranscodingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    TransmuxingEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RealtimeMonitorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AutoScanIntervalHours = table.Column<int>(type: "integer", nullable: false, defaultValue: 6),
                    LibraryGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Libraries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Libraries_LibraryGroups_LibraryGroupId",
                        column: x => x.LibraryGroupId,
                        principalTable: "LibraryGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Libraries_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Medias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    SortTitle = table.Column<string>(type: "text", nullable: true),
                    OriginalTitle = table.Column<string>(type: "text", nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastMetadataRefreshedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedFields = table.Column<string[]>(type: "text[]", nullable: false),
                    Tagline = table.Column<string>(type: "text", nullable: true),
                    Overview = table.Column<string>(type: "text", nullable: true),
                    OriginalLanguage = table.Column<string>(type: "text", nullable: true),
                    Budget = table.Column<long>(type: "bigint", nullable: true),
                    Revenue = table.Column<long>(type: "bigint", nullable: true),
                    MusicAlbum_Overview = table.Column<string>(type: "text", nullable: true),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtistType = table.Column<int>(type: "integer", nullable: true),
                    Biography = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    MusicTrack_ArtistId = table.Column<Guid>(type: "uuid", nullable: true),
                    TrackNumber = table.Column<int>(type: "integer", nullable: true),
                    DiscNumber = table.Column<int>(type: "integer", nullable: true),
                    Lyrics = table.Column<string>(type: "text", nullable: true),
                    LyricsLrc = table.Column<string>(type: "text", nullable: true),
                    Serie_Overview = table.Column<string>(type: "text", nullable: true),
                    Serie_OriginalLanguage = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    SerieId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: true),
                    EpisodeNumber = table.Column<int>(type: "integer", nullable: true),
                    SerieEpisode_Overview = table.Column<string>(type: "text", nullable: true),
                    AbsoluteNumber = table.Column<int>(type: "integer", nullable: true),
                    AirDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Runtime = table.Column<int>(type: "integer", nullable: true),
                    SerieSeason_SerieId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: true),
                    SerieSeason_Overview = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true),
                    Trailers = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_MusicTrack_ArtistId",
                        column: x => x.MusicTrack_ArtistId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Medias_Medias_SerieSeason_SerieId",
                        column: x => x.SerieSeason_SerieId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PeerSocialAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AllowOutbound = table.Column<bool>(type: "boolean", nullable: false),
                    AllowInbound = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerSocialAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerSocialAgreements_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    Biography = table.Column<string>(type: "text", nullable: true),
                    Birthday = table.Column<DateOnly>(type: "date", nullable: true),
                    Deathday = table.Column<DateOnly>(type: "date", nullable: true),
                    BirthPlace = table.Column<string>(type: "text", nullable: true),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedFields = table.Column<string[]>(type: "text[]", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Persons_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityUserId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PinHash = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContentRestrictionProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_ContentRestrictionProfiles_ContentRestrictionProfileId",
                        column: x => x.ContentRestrictionProfileId,
                        principalTable: "ContentRestrictionProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Users_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    AuthorizationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Library_ScanIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Library_ScanIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Library_ScanIssues_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeerShareAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxConcurrentStreams = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SharePlaybackHistory = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerShareAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeerShareAgreements_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeerShareAgreements_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioAnalysis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MusicTrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChromaprintFingerprint = table.Column<string>(type: "text", nullable: true),
                    ChromaprintDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    AcoustId = table.Column<string>(type: "text", nullable: true),
                    AcoustIdScore = table.Column<double>(type: "double precision", nullable: true),
                    LoudnessLufs = table.Column<double>(type: "double precision", nullable: true),
                    WaveformPeaks = table.Column<string>(type: "jsonb", nullable: true),
                    FadeInDuration = table.Column<double>(type: "double precision", nullable: true),
                    FadeOutDuration = table.Column<double>(type: "double precision", nullable: true),
                    ReplayGainTrackGain = table.Column<double>(type: "double precision", nullable: true),
                    ReplayGainAlbumGain = table.Column<double>(type: "double precision", nullable: true),
                    AnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnalysisVersion = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioAnalysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioAnalysis_Medias_MusicTrackId",
                        column: x => x.MusicTrackId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndexedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Extension = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    ParentDirectory = table.Column<string>(type: "text", nullable: true),
                    Hash = table.Column<long>(type: "bigint", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    LastWriteTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Identification_Title = table.Column<string>(type: "text", nullable: true),
                    Identification_ReleaseYear = table.Column<DateOnly>(type: "date", nullable: true),
                    Identification_TrackNumber = table.Column<int>(type: "integer", nullable: true),
                    Identification_AlbumName = table.Column<string>(type: "text", nullable: true),
                    Identification_ArtistName = table.Column<string>(type: "text", nullable: true),
                    Identification_SeriesTitle = table.Column<string>(type: "text", nullable: true),
                    Identification_SeasonNumber = table.Column<int>(type: "integer", nullable: true),
                    Identification_EpisodeNumber = table.Column<int>(type: "integer", nullable: true),
                    Identification_AbsoluteNumber = table.Column<int>(type: "integer", nullable: true),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChromaprintFingerprint = table.Column<byte[]>(type: "bytea", nullable: true),
                    ChromaprintDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ChromaprintAnalyzedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexedFiles_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndexedFiles_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MediaLibraryAvailabilities",
                columns: table => new
                {
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaLibraryAvailabilities", x => new { x.LibraryId, x.MediaId });
                    table.ForeignKey(
                        name: "FK_MediaLibraryAvailabilities_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaLibraryAvailabilities_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaMetadataTags",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetadataTagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaMetadataTags", x => new { x.MediaId, x.MetadataTagId });
                    table.ForeignKey(
                        name: "FK_MediaMetadataTags_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaMetadataTags_MetadataTags_MetadataTagId",
                        column: x => x.MetadataTagId,
                        principalTable: "MetadataTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaRecommendations",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RecommendedIds = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaRecommendations", x => new { x.MediaId, x.ProviderName });
                    table.ForeignKey(
                        name: "FK_MediaRecommendations_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    StartMs = table.Column<long>(type: "bigint", nullable: false),
                    EndMs = table.Column<long>(type: "bigint", nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaSegments_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicArtistCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MusicArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsGuest = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: true),
                    MusicAlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicArtistCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MusicArtistCredits_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MusicArtistCredits_Medias_MusicAlbumId",
                        column: x => x.MusicAlbumId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MusicArtistCredits_Medias_MusicArtistId",
                        column: x => x.MusicArtistId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemoteIndexedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemoteFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Extension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Container = table.Column<string>(type: "text", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    VideoBitrate = table.Column<long>(type: "bigint", nullable: true),
                    VideoResolution = table.Column<int>(type: "integer", nullable: true),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemoteMediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemoteLibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemoteIndexedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemoteIndexedFiles_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemoteIndexedFiles_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RemoteIndexedFiles_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterName = table.Column<string>(type: "text", nullable: true),
                    Department = table.Column<string>(type: "text", nullable: true),
                    Job = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: true),
                    VoiceActor_CharacterName = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonRoles_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonRoles_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    VisibilityScope = table.Column<int>(type: "integer", nullable: false),
                    MediaType = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Collections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceUser",
                columns: table => new
                {
                    DevicesId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceUser", x => new { x.DevicesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_DeviceUser_Devices_DevicesId",
                        column: x => x.DevicesId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceUser_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaPlaybackSessionCoViewers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaPlaybackSessionCoViewers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaPlaybackSessionCoViewers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MediaType = table.Column<int>(type: "integer", nullable: false),
                    VisibilityScope = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Discriminator = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    RuleFilter = table.Column<string>(type: "jsonb", nullable: true),
                    Limit = table.Column<int>(type: "integer", nullable: true),
                    OrderBy = table.Column<int>(type: "integer", nullable: true),
                    OrderDescending = table.Column<bool>(type: "boolean", nullable: true),
                    LastEvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    MinimumValue = table.Column<double>(type: "double precision", nullable: false),
                    MaximumValue = table.Column<double>(type: "double precision", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetadataProvider = table.Column<int>(type: "integer", nullable: true),
                    RatingCount = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ratings_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PinHash = table.Column<string>(type: "text", nullable: true),
                    ContentRestrictionProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedProfiles_ContentRestrictionProfiles_ContentRestrictio~",
                        column: x => x.ContentRestrictionProfileId,
                        principalTable: "ContentRestrictionProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SharedProfiles_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedProfiles_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserCapabilityOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCapabilityOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCapabilityOverrides_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLibraryExclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAdminExcluded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsSelfExcluded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLibraryExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLibraryExclusions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMediaExclusions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAdminExcluded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsSelfExcluded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMediaExclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMediaExclusions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMediaStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastPlaybackPosition = table.Column<double>(type: "double precision", nullable: false),
                    ProgressPercentage = table.Column<double>(type: "double precision", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    LastInteractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastKnownDurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    ExcludedFromContinueWatching = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMediaStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMediaStates_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMediaStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisibilityGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<int>(type: "integer", nullable: true),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetPeerServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetOriginUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisibilityGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisibilityGrants_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Downloads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OutputPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    AudioTrackIndex = table.Column<int>(type: "integer", nullable: true),
                    SubtitleTrackIndices = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsDirectStream = table.Column<bool>(type: "boolean", nullable: false),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Downloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Downloads_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Downloads_IndexedFiles_IndexedFileId",
                        column: x => x.IndexedFileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Downloads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FileMetadatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Container = table.Column<string>(type: "text", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    VideoBitrate = table.Column<long>(type: "bigint", nullable: true),
                    VideoFileMetadata_Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    VideoResolution = table.Column<int>(type: "integer", nullable: true),
                    Chapters = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMetadatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileMetadatas_IndexedFiles_IndexedFileId",
                        column: x => x.IndexedFileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    RemoteIndexedFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeerServerId = table.Column<Guid>(type: "uuid", nullable: true),
                    RemoteSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RootDirectory = table.Column<string>(type: "text", nullable: false),
                    PlaybackSettingsJson = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_IndexedFiles_IndexedFileId",
                        column: x => x.IndexedFileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_PeerServers_PeerServerId",
                        column: x => x.PeerServerId,
                        principalTable: "PeerServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_RemoteIndexedFiles_RemoteIndexedFileId",
                        column: x => x.RemoteIndexedFileId,
                        principalTable: "RemoteIndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ExternalIds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalIds_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalIds_PersonRoles_PersonRoleId",
                        column: x => x.PersonRoleId,
                        principalTable: "PersonRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalIds_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CollectionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionItems_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollectionItems_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPlaylistStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastListenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPlaylistStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPlaylistStates_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPlaylistStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserRatingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaReviews_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaReviews_Ratings_UserRatingId",
                        column: x => x.UserRatingId,
                        principalTable: "Ratings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaPlaybackSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StoppedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PositionSeconds = table.Column<double>(type: "double precision", nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    WatchedDurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedProfileNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CoWatchingWithSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaPlaybackSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaPlaybackSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MediaPlaybackSessions_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaPlaybackSessions_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MediaPlaybackSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedProfileMediaStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastPlaybackPosition = table.Column<double>(type: "double precision", nullable: false),
                    ProgressPercentage = table.Column<double>(type: "double precision", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    LastInteractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastKnownDurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    ExcludedFromContinueWatching = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfileMediaStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedProfileMediaStates_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedProfileMediaStates_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedProfileMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfileMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedProfileMembers_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedProfileMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedProfilePlaylists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedProfilePlaylists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedProfilePlaylists_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedProfilePlaylists_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    AudioFileMetadataId = table.Column<Guid>(type: "uuid", nullable: true),
                    VideoFileMetadataId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Codec = table.Column<string>(type: "text", nullable: true),
                    Channels = table.Column<int>(type: "integer", nullable: true),
                    ChannelLayout = table.Column<string>(type: "text", nullable: true),
                    SampleRateHz = table.Column<int>(type: "integer", nullable: true),
                    Profile = table.Column<string>(type: "text", nullable: true),
                    SubtitleFileTrack_VideoFileMetadataId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubtitleFileTrack_Name = table.Column<string>(type: "text", nullable: true),
                    SubtitleFileTrack_Language = table.Column<string>(type: "text", nullable: true),
                    SubtitleFileTrack_Codec = table.Column<string>(type: "text", nullable: true),
                    IsTextBased = table.Column<bool>(type: "boolean", nullable: true),
                    IsForced = table.Column<bool>(type: "boolean", nullable: true),
                    IsHearingImpaired = table.Column<bool>(type: "boolean", nullable: true),
                    VideoFileTrack_VideoFileMetadataId = table.Column<Guid>(type: "uuid", nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    VideoFileTrack_Codec = table.Column<string>(type: "text", nullable: true),
                    VideoFileTrack_Profile = table.Column<string>(type: "text", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: true),
                    PixelFormat = table.Column<string>(type: "text", nullable: true),
                    BitDepth = table.Column<int>(type: "integer", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_AudioFileMetadataId",
                        column: x => x.AudioFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadat~",
                        column: x => x.SubtitleFileTrack_VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_VideoFileMetadataId",
                        column: x => x.VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_VideoFileTrack_VideoFileMetadataId",
                        column: x => x.VideoFileTrack_VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HlsSegments",
                columns: table => new
                {
                    FileMetadataId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    Duration = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HlsSegments", x => new { x.FileMetadataId, x.Number });
                    table.ForeignKey(
                        name: "FK_HlsSegments_FileMetadatas_FileMetadataId",
                        column: x => x.FileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetadataPictures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OriginalRemoteUri = table.Column<string>(type: "text", nullable: true),
                    LocalPath = table.Column<string>(type: "text", nullable: true),
                    OriginalWidth = table.Column<int>(type: "integer", nullable: true),
                    OriginalHeight = table.Column<int>(type: "integer", nullable: true),
                    DominantColor = table.Column<string>(type: "text", nullable: true),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    VideoFileMetadataId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    PersonRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LibraryGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataPictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_Collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "Collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                        column: x => x.VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_LibraryGroups_LibraryGroupId",
                        column: x => x.LibraryGroupId,
                        principalTable: "LibraryGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MetadataPictures_PersonRoles_PersonRoleId",
                        column: x => x.PersonRoleId,
                        principalTable: "PersonRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MetadataPictures_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_SharedProfiles_SharedProfileId",
                        column: x => x.SharedProfileId,
                        principalTable: "SharedProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EphemeralStreamTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StreamSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EphemeralStreamTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EphemeralStreamTokens_StreamSessions_StreamSessionId",
                        column: x => x.StreamSessionId,
                        principalTable: "StreamSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EphemeralStreamTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaybackSessionDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaPlaybackSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsTranscode = table.Column<bool>(type: "boolean", nullable: true),
                    VideoDecision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AudioDecision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TranscodeReason = table.Column<int>(type: "integer", nullable: true),
                    Bitrate = table.Column<int>(type: "integer", nullable: true),
                    SourceVideoCodec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceAudioCodec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceVideoWidth = table.Column<int>(type: "integer", nullable: true),
                    SourceVideoHeight = table.Column<int>(type: "integer", nullable: true),
                    StreamVideoCodec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StreamAudioCodec = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AudioTrackLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AudioTrackTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AudioChannelLayout = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubtitleTrackLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SubtitleTrackTitle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackSessionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackSessionDetails_MediaPlaybackSessions_MediaPlaybackS~",
                        column: x => x.MediaPlaybackSessionId,
                        principalTable: "MediaPlaybackSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetadataPictureVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    LocalPath = table.Column<string>(type: "text", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    MetadataPictureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataPictureVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataPictureVariants_MetadataPictures_MetadataPictureId",
                        column: x => x.MetadataPictureId,
                        principalTable: "MetadataPictures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyPrefix",
                table: "ApiKeys",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioAnalysis_MusicTrackId",
                table: "AudioAnalysis",
                column: "MusicTrackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_Lane",
                table: "BackgroundTasks",
                column: "Lane");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_Status_WorkClass_Priority_Created",
                table: "BackgroundTasks",
                columns: new[] { "Status", "WorkClass", "Priority", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundTasks_TargetEntityId",
                table: "BackgroundTasks",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "UX_BackgroundTasks_Name_TargetEntityId_Active",
                table: "BackgroundTasks",
                columns: new[] { "Name", "TargetEntityId" },
                unique: true,
                filter: "\"Status\" IN (0, 1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionItems_CollectionId",
                table: "CollectionItems",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionItems_MediaId",
                table: "CollectionItems",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Collections_UserId",
                table: "Collections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUser_UsersId",
                table: "DeviceUser",
                column: "UsersId");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_DeviceId",
                table: "Downloads",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_IndexedFileId_DeviceId_UserId",
                table: "Downloads",
                columns: new[] { "IndexedFileId", "DeviceId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_UserId",
                table: "Downloads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_ExpiresAt",
                table: "EphemeralStreamTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_StreamSessionId",
                table: "EphemeralStreamTokens",
                column: "StreamSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_Token",
                table: "EphemeralStreamTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EphemeralStreamTokens_UserId",
                table: "EphemeralStreamTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_MediaId",
                table: "ExternalIds",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_PersonId",
                table: "ExternalIds",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_PersonRoleId",
                table: "ExternalIds",
                column: "PersonRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_ProviderName_Value",
                table: "ExternalIds",
                columns: new[] { "ProviderName", "Value" });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_IndexedFileId",
                table: "FileMetadatas",
                column: "IndexedFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_AudioFileMetadataId",
                table: "FileTracks",
                column: "AudioFileMetadataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "SubtitleFileTrack_VideoFileMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_VideoFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileTrack_VideoFileMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_HlsSegments_IndexedFileId_Number",
                table: "HlsSegments",
                columns: new[] { "IndexedFileId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_Hash",
                table: "IndexedFiles",
                column: "Hash");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_LibraryId_Created",
                table: "IndexedFiles",
                columns: new[] { "LibraryId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_MediaId",
                table: "IndexedFiles",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_Path",
                table: "IndexedFiles",
                column: "Path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_LibraryGroupId",
                table: "Libraries",
                column: "LibraryGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Libraries_PeerServerId",
                table: "Libraries",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Library_ScanIssues_LibraryId",
                table: "Library_ScanIssues",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLibraryAvailabilities_LibraryId",
                table: "MediaLibraryAvailabilities",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLibraryAvailabilities_MediaId",
                table: "MediaLibraryAvailabilities",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaMetadataTags_MetadataTagId",
                table: "MediaMetadataTags",
                column: "MetadataTagId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessionCoViewers_ReferenceId_UserId",
                table: "MediaPlaybackSessionCoViewers",
                columns: new[] { "ReferenceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessionCoViewers_UserId",
                table: "MediaPlaybackSessionCoViewers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_DeviceId",
                table: "MediaPlaybackSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_MediaId",
                table: "MediaPlaybackSessions",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_ReferenceId",
                table: "MediaPlaybackSessions",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_SessionId",
                table: "MediaPlaybackSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_SharedProfileId",
                table: "MediaPlaybackSessions",
                column: "SharedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId_CompletedAt",
                table: "MediaPlaybackSessions",
                columns: new[] { "UserId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId_MediaId_CompletedAt",
                table: "MediaPlaybackSessions",
                columns: new[] { "UserId", "MediaId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId_StartedAt",
                table: "MediaPlaybackSessions",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaReviews_MediaId_UserId",
                table: "MediaReviews",
                columns: new[] { "MediaId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaReviews_UserId",
                table: "MediaReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaReviews_UserRatingId",
                table: "MediaReviews",
                column: "UserRatingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medias_AlbumId",
                table: "Medias",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_ArtistId",
                table: "Medias",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_MusicTrack_ArtistId",
                table: "Medias",
                column: "MusicTrack_ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_OriginalTitle",
                table: "Medias",
                column: "OriginalTitle");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_PeerServerId",
                table: "Medias",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_ReleaseDate",
                table: "Medias",
                column: "ReleaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SeasonId_EpisodeNumber",
                table: "Medias",
                columns: new[] { "SeasonId", "EpisodeNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SerieId",
                table: "Medias",
                column: "SerieId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SerieSeason_SerieId_SeasonNumber",
                table: "Medias",
                columns: new[] { "SerieSeason_SerieId", "SeasonNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SortTitle",
                table: "Medias",
                column: "SortTitle");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Title_trgm",
                table: "Medias",
                column: "Title",
                filter: "\"Title\" IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Type",
                table: "Medias",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_MediaSegments_MediaId",
                table: "MediaSegments",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaSegments_MediaId_Type",
                table: "MediaSegments",
                columns: new[] { "MediaId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_CollectionId",
                table: "MetadataPictures",
                column: "CollectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_LibraryGroupId",
                table: "MetadataPictures",
                column: "LibraryGroupId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_MediaId",
                table: "MetadataPictures",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_PersonId",
                table: "MetadataPictures",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_PersonRoleId",
                table: "MetadataPictures",
                column: "PersonRoleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_PlaylistId",
                table: "MetadataPictures",
                column: "PlaylistId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_SharedProfileId",
                table: "MetadataPictures",
                column: "SharedProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_UserId",
                table: "MetadataPictures",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictureVariants_MetadataPictureId_Size",
                table: "MetadataPictureVariants",
                columns: new[] { "MetadataPictureId", "Size" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataTags_Kind_NormalizedKey",
                table: "MetadataTags",
                columns: new[] { "Kind", "NormalizedKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MusicArtistCredits_MediaId",
                table: "MusicArtistCredits",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicArtistCredits_MusicAlbumId",
                table: "MusicArtistCredits",
                column: "MusicAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicArtistCredits_MusicArtistId_MediaId",
                table: "MusicArtistCredits",
                columns: new[] { "MusicArtistId", "MediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationRules_IsEnabled",
                table: "NotificationRules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                table: "OpenIddictTokens",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeerShareAgreements_LibraryId",
                table: "PeerShareAgreements",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerShareAgreements_PeerServerId",
                table: "PeerShareAgreements",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerSocialAgreements_PeerServerId_ContentType",
                table: "PeerSocialAgreements",
                columns: new[] { "PeerServerId", "ContentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonRoles_CharacterName_trgm",
                table: "PersonRoles",
                column: "CharacterName",
                filter: "\"CharacterName\" IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonRoles_MediaId",
                table: "PersonRoles",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonRoles_PersonId",
                table: "PersonRoles",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonRoles_VoiceActor_CharacterName_trgm",
                table: "PersonRoles",
                column: "VoiceActor_CharacterName",
                filter: "\"VoiceActor_CharacterName\" IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Persons_Name_trgm",
                table: "Persons",
                column: "Name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Persons_PeerServerId",
                table: "Persons",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackSessionDetails_MediaPlaybackSessionId",
                table: "PlaybackSessionDetails",
                column: "MediaPlaybackSessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_MediaId",
                table: "PlaylistItems",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId_Order",
                table: "PlaylistItems",
                columns: new[] { "PlaylistId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_UserId",
                table: "Playlists",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_MediaId",
                table: "Ratings",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_MediaId_UserId",
                table: "Ratings",
                columns: new[] { "MediaId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId",
                table: "Ratings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_LibraryId_Created",
                table: "RemoteIndexedFiles",
                columns: new[] { "LibraryId", "Created" });

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_MediaId",
                table: "RemoteIndexedFiles",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_RemoteIndexedFiles_PeerServerId",
                table: "RemoteIndexedFiles",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_MediaId",
                table: "SharedProfileMediaStates",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_IsCompleted_LastInteractedAt",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "IsCompleted", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_LastInteractedAt",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_MediaId",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "MediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMediaStates_SharedProfileId_PlayCount",
                table: "SharedProfileMediaStates",
                columns: new[] { "SharedProfileId", "PlayCount" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMembers_SharedProfileId_UserId",
                table: "SharedProfileMembers",
                columns: new[] { "SharedProfileId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileMembers_UserId",
                table: "SharedProfileMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfilePlaylists_PlaylistId",
                table: "SharedProfilePlaylists",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfilePlaylists_SharedProfileId_PlaylistId",
                table: "SharedProfilePlaylists",
                columns: new[] { "SharedProfileId", "PlaylistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfiles_ContentRestrictionProfileId",
                table: "SharedProfiles",
                column: "ContentRestrictionProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfiles_CreatedByUserId",
                table: "SharedProfiles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfiles_HostUserId",
                table: "SharedProfiles",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedProfileSettings_SharedProfileId_Key",
                table: "SharedProfileSettings",
                columns: new[] { "SharedProfileId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_DeviceId",
                table: "StreamSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_IndexedFileId",
                table: "StreamSessions",
                column: "IndexedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_PeerServerId",
                table: "StreamSessions",
                column: "PeerServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_RemoteIndexedFileId",
                table: "StreamSessions",
                column: "RemoteIndexedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_UserId",
                table: "StreamSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncPlayInvites_CreatedAt",
                table: "SyncPlayInvites",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncPlayInvites_GroupId",
                table: "SyncPlayInvites",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncPlayInvites_Token",
                table: "SyncPlayInvites",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCapabilityOverrides_UserId",
                table: "UserCapabilityOverrides",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryExclusions_UserId_LibraryId",
                table: "UserLibraryExclusions",
                columns: new[] { "UserId", "LibraryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaExclusions_UserId_MediaId",
                table: "UserMediaExclusions",
                columns: new[] { "UserId", "MediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_MediaId",
                table: "UserMediaStates",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_IsCompleted_LastInteractedAt",
                table: "UserMediaStates",
                columns: new[] { "UserId", "IsCompleted", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_LastInteractedAt",
                table: "UserMediaStates",
                columns: new[] { "UserId", "LastInteractedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_MediaId",
                table: "UserMediaStates",
                columns: new[] { "UserId", "MediaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId_PlayCount",
                table: "UserMediaStates",
                columns: new[] { "UserId", "PlayCount" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlaylistStates_PlaylistId",
                table: "UserPlaylistStates",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPlaylistStates_UserId_LastListenedAt",
                table: "UserPlaylistStates",
                columns: new[] { "UserId", "LastListenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPlaylistStates_UserId_PlaylistId",
                table: "UserPlaylistStates",
                columns: new[] { "UserId", "PlaylistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ContentRestrictionProfileId",
                table: "Users",
                column: "ContentRestrictionProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PeerServerId_OriginUserId",
                table: "Users",
                columns: new[] { "PeerServerId", "OriginUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisibilityGrants_OwnerUserId",
                table: "VisibilityGrants",
                column: "OwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AudioAnalysis");

            migrationBuilder.DropTable(
                name: "BackgroundTasks");

            migrationBuilder.DropTable(
                name: "CollectionItems");

            migrationBuilder.DropTable(
                name: "DeviceUser");

            migrationBuilder.DropTable(
                name: "Downloads");

            migrationBuilder.DropTable(
                name: "EphemeralStreamTokens");

            migrationBuilder.DropTable(
                name: "ExternalIds");

            migrationBuilder.DropTable(
                name: "FileTracks");

            migrationBuilder.DropTable(
                name: "HlsSegments");

            migrationBuilder.DropTable(
                name: "Library_ScanIssues");

            migrationBuilder.DropTable(
                name: "MediaLibraryAvailabilities");

            migrationBuilder.DropTable(
                name: "MediaMetadataTags");

            migrationBuilder.DropTable(
                name: "MediaPlaybackSessionCoViewers");

            migrationBuilder.DropTable(
                name: "MediaRecommendations");

            migrationBuilder.DropTable(
                name: "MediaReviews");

            migrationBuilder.DropTable(
                name: "MediaSegments");

            migrationBuilder.DropTable(
                name: "MetadataPictureVariants");

            migrationBuilder.DropTable(
                name: "MusicArtistCredits");

            migrationBuilder.DropTable(
                name: "NotificationRules");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "PeerRequests");

            migrationBuilder.DropTable(
                name: "PeerShareAgreements");

            migrationBuilder.DropTable(
                name: "PeerSocialAgreements");

            migrationBuilder.DropTable(
                name: "PlaybackSessionDetails");

            migrationBuilder.DropTable(
                name: "PlaylistItems");

            migrationBuilder.DropTable(
                name: "ServerSettings");

            migrationBuilder.DropTable(
                name: "SharedProfileMediaStates");

            migrationBuilder.DropTable(
                name: "SharedProfileMembers");

            migrationBuilder.DropTable(
                name: "SharedProfilePlaylists");

            migrationBuilder.DropTable(
                name: "SharedProfileSettings");

            migrationBuilder.DropTable(
                name: "SyncPlayInvites");

            migrationBuilder.DropTable(
                name: "UserCapabilityOverrides");

            migrationBuilder.DropTable(
                name: "UserLibraryExclusions");

            migrationBuilder.DropTable(
                name: "UserMediaExclusions");

            migrationBuilder.DropTable(
                name: "UserMediaStates");

            migrationBuilder.DropTable(
                name: "UserPlaylistStates");

            migrationBuilder.DropTable(
                name: "UserSettings");

            migrationBuilder.DropTable(
                name: "VisibilityGrants");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "StreamSessions");

            migrationBuilder.DropTable(
                name: "MetadataTags");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "MetadataPictures");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "MediaPlaybackSessions");

            migrationBuilder.DropTable(
                name: "RemoteIndexedFiles");

            migrationBuilder.DropTable(
                name: "Collections");

            migrationBuilder.DropTable(
                name: "FileMetadatas");

            migrationBuilder.DropTable(
                name: "PersonRoles");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "SharedProfiles");

            migrationBuilder.DropTable(
                name: "IndexedFiles");

            migrationBuilder.DropTable(
                name: "Persons");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Libraries");

            migrationBuilder.DropTable(
                name: "Medias");

            migrationBuilder.DropTable(
                name: "ContentRestrictionProfiles");

            migrationBuilder.DropTable(
                name: "LibraryGroups");

            migrationBuilder.DropTable(
                name: "PeerServers");
        }
    }
}
