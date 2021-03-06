﻿using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codity.Data.Model;
using Codity.Repositories.Interfaces;
using Codity.Services.Interfaces;
using Codity.Services.RequestModels;
using Codity.Services.RequestModels.User;
using Codity.Services.Resources;
using Codity.Services.ResponseModels;
using Codity.Services.ResponseModels.DTOs.User;
using Codity.Services.ResponseModels.Interfaces;
using Codity.Services.ResponseModels.DTOs.Post;

namespace Codity.Services.Services
{
    public class UserService : IUserService
    {
        private readonly IBaseRepository<PostLike> _postLikeRepository;
        private readonly IBaseRepository<Follow> _followRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationGeneratorService _notificationGeneratorService;
        private readonly IMapper _mapper;

        public UserService(
            IBaseRepository<PostLike> postLikeRepository,
            IBaseRepository<Follow> followRepository,
            IUserRepository userRepository,
            INotificationGeneratorService notificationGeneratorService,
            IMapper mapper)
        {
            _postLikeRepository = postLikeRepository;
            _followRepository = followRepository;
            _userRepository = userRepository;
            _notificationGeneratorService = notificationGeneratorService;
            _mapper = mapper;
        }

        public async Task<IResponse<UserDTO>> GetUserAsync(int userId, int currentUserId)
        {
            var response = new Response<UserDTO>();

            var user = await _userRepository.GetByAsync(
                c => c.Id == userId,
                false);

            if (user == null)
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.UserNotFound
                });

                return response;
            }

            var userDTO = _mapper.Map<UserDTO>(user);

            userDTO.LatestPosts = _mapper.Map<IEnumerable<PostDTO>>(user.Posts.OrderByDescending(c => c.CreationDate).Take(5));
            var postIds = userDTO.LatestPosts.Select(c => c.Id);
            var likes = await _postLikeRepository.GetAllByAsync(c => postIds.Contains(c.PostId) && c.UserId == currentUserId);
            foreach (var post in userDTO.LatestPosts)
            {
                post.IsLiked = likes.Any(c => c.PostId == post.Id);
            }

            userDTO.IsFollowing = await _followRepository.ExistAsync(c => c.FollowerId == currentUserId && c.FollowingId == userId);

            response.Model = userDTO;

            return response;
        }

        public async Task<IBaseResponse> UnfollowUserAsync(int userId, FollowingRequest following)
        {
            var response = new BaseResponse();

            var follow = await _followRepository
                .GetByAsync(c => c.FollowerId == userId && c.FollowingId == following.FollowingId);

            if (follow == null)
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.FollowNotFound
                });

                return response;
            }

            await _followRepository.RemoveAsync(follow);

            return response;
        }

        public async Task<IBaseResponse> FollowUserAsync(int userId, FollowingRequest following)
        {
            var response = new BaseResponse();

            if (userId == following.FollowingId)
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.FollowingYourself
                });

                return response;
            }

            var followerUser = await _userRepository.GetAsync(userId);
            var followingUser = await _userRepository.GetAsync(following.FollowingId);

            if (followerUser == null)
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.UserNotFound
                });

                return response;
            }

            if (followingUser == null)
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.UserNotFound
                });

                return response;
            }

            var follow = await _followRepository
                .GetByAsync(c => c.FollowerId == userId && c.FollowingId == following.FollowingId);

            if (follow != null)
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.FollowAlreadyExists
                });

                return response;
            }

            follow = new Follow
            {
                FollowerId = userId,
                FollowingId = following.FollowingId
            };

            await _followRepository.AddAsync(follow);

            await _notificationGeneratorService.CreateFollowNotification(followerUser, followingUser);

            return response;
        }

        public async Task<IPagedResponse<BaseUserDTO>> GetFollowersAsync(int userId, int currentUserId, PaginationRequest paginationRequest)
        {
            var response = new PagedResponse<BaseUserDTO>();

            var follows = await _followRepository.GetPagedByAsync(
                c => c.FollowingId == userId,
                paginationRequest.PageNumber,
                paginationRequest.PageSize,
                false,
                c => c.Follower);

            if (!follows.Any())
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.FollowersNotFound
                });

                return response;
            }

            _mapper.Map(follows, response);

            var followers = follows.Select(c => c.Follower);
            response.Models = _mapper.Map<IEnumerable<BaseUserDTO>>(followers);

            var userIds = followers.Select(c => c.Id);
            var following = await _followRepository
                .GetAllByAsync(c => userIds.Contains(c.FollowingId) && c.FollowerId == currentUserId);

            foreach (var model in response.Models)
            {
                model.IsFollowing = following.Any(c => c.FollowingId == model.Id);
            }

            return response;
        }

        public async Task<IPagedResponse<BaseUserDTO>> GetFollowingAsync(int userId, int currentUserId, PaginationRequest paginationRequest)
        {
            var response = new PagedResponse<BaseUserDTO>();

            var follows = await _followRepository.GetPagedByAsync(
                c => c.FollowerId == userId,
                paginationRequest.PageNumber,
                paginationRequest.PageSize,
                false,
                c => c.Following);

            if (!follows.Any())
            {
                response.AddError(new Error
                {
                    Message = ErrorTranslations.FollowingNotFound
                });

                return response;
            }

            _mapper.Map(follows, response);

            var following = follows.Select(c => c.Following);
            response.Models = _mapper.Map<IEnumerable<BaseUserDTO>>(following);

            var userIds = following.Select(c => c.Id);
            var currentUsersFollowing = await _followRepository
                .GetAllByAsync(c => userIds.Contains(c.FollowingId) && c.FollowerId == currentUserId);

            foreach (var model in response.Models)
            {
                model.IsFollowing = currentUsersFollowing.Any(c => c.FollowingId == model.Id);
            }

            return response;
        }

        public async Task<IPagedResponse<BaseUserDTO>> GetUsersAsync(SearchUserRequest searchRequest, int currentUserId)
        {
            var response = new PagedResponse<BaseUserDTO>();

            var users = await _userRepository.SearchAsync(
                searchRequest.Query,
                searchRequest.PageNumber,
                searchRequest.PageSize,
                currentUserId);

            _mapper.Map(users, response);

            var userIds = users.Select(c => c.Id);
            var following = await _followRepository.GetAllByAsync(c => userIds.Contains(c.FollowingId) && c.FollowerId == currentUserId);
            foreach (var model in response.Models)
            {
                model.IsFollowing = following.Any(c => c.FollowingId == model.Id);
            }

            return response;
        }

        public async Task<IBaseResponse> UpdateUserProfileAsync(int userId, UserProfileRequest userProfile)
        {
            var response = new BaseResponse();

            var user = await _userRepository.GetAsync(userId);

            _mapper.Map(userProfile, user);

            await _userRepository.UpdateAsync(user);

            return response;
        }
    }
}
