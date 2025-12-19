using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ably_rest_apis.src.Api.DTOs;
using ably_rest_apis.src.Domain.Entities;
using ably_rest_apis.src.Domain.Enums;
using ably_rest_apis.src.Infrastructure.Persistence.DbContext;

namespace ably_rest_apis.src.Api.Controllers
{
    /// <summary>
    /// User management controller for testing purposes
    /// </summary>
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<UsersController> _logger;

        public UsersController(ApplicationDbContext dbContext, ILogger<UsersController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public class CreateUserRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = "Student";
        }

        public class UserResponse
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        /// <summary>
        /// Creates a new user
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<UserResponse>>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                // Check if email exists
                var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
                if (existingUser != null)
                {
                    return BadRequest(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = "Email already exists"
                    });
                }

                // Parse role
                if (!Enum.TryParse<Role>(request.Role, true, out var role))
                {
                    return BadRequest(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = $"Invalid role. Valid roles: {string.Join(", ", Enum.GetNames<Role>())}"
                    });
                }

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Email = request.Email,
                    Role = role,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Created user {UserId} with role {Role}", user.Id, role);

                return Ok(new ApiResponse<UserResponse>
                {
                    Success = true,
                    Message = "User created successfully",
                    Data = new UserResponse
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        Role = user.Role.ToString(),
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new ApiResponse<UserResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets a user by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<UserResponse>>> GetUser(Guid id)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponse<UserResponse>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<UserResponse>
                {
                    Success = true,
                    Data = new UserResponse
                    {
                        Id = user.Id,
                        Name = user.Name,
                        Email = user.Email,
                        Role = user.Role.ToString(),
                        CreatedAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", id);
                return StatusCode(500, new ApiResponse<UserResponse>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets all users
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetAllUsers()
        {
            try
            {
                var users = await _dbContext.Users
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                var response = users.Select(u => new UserResponse
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(new ApiResponse<List<UserResponse>>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new ApiResponse<List<UserResponse>>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        /// <summary>
        /// Gets users by role
        /// </summary>
        [HttpGet("by-role/{role}")]
        public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetUsersByRole(string role)
        {
            try
            {
                if (!Enum.TryParse<Role>(role, true, out var roleEnum))
                {
                    return BadRequest(new ApiResponse<List<UserResponse>>
                    {
                        Success = false,
                        Message = $"Invalid role. Valid roles: {string.Join(", ", Enum.GetNames<Role>())}"
                    });
                }

                var users = await _dbContext.Users
                    .Where(u => u.Role == roleEnum)
                    .OrderBy(u => u.Name)
                    .ToListAsync();

                var response = users.Select(u => new UserResponse
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(new ApiResponse<List<UserResponse>>
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users by role {Role}", role);
                return StatusCode(500, new ApiResponse<List<UserResponse>>
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }
    }
}
