﻿using ScmBackup.Hosters;
using ScmBackup.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Xunit;

namespace ScmBackup.Tests.Integration.Hosters
{
    public abstract class IHosterApiTests
    {
        // environment variables with this prefix (from environment-variables.ps1) are used
        internal abstract string EnvVarPrefix { get; }

        // "hoster" value for config sources
        internal abstract string ConfigHoster { get; }

        // minimum number of repos which must be returned in "GetRepositoryList_PaginationWorks" -> should be more than the default page size of the hoster's API
        internal abstract int Pagination_MinNumberOfRepos { get; }

        // set this to true in order to skip all tests without authentication (because of rate limits, see #7)
        internal abstract bool SkipUnauthenticatedTests { get; }

        // this needs to be created in the child classes' constructor:
        internal IHosterApi sut;

        [Fact]
        public void SutWasSetInChildClass()
        {
            Assert.NotNull(this.sut);
        }

        [SkippableFact]
        public void GetRepositoryList_UnauthenticatedUser_Executes()
        {
            Skip.If(this.SkipUnauthenticatedTests);

            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "user";
            source.Name = TestHelper.EnvVar(this.EnvVarPrefix, "Name");

            var repoList = this.sut.GetRepositoryList(source);

            // at least one result?
            Assert.NotNull(repoList);
            Assert.True(repoList.Count > 0);

            // specific repo exists?
            string expectedName = TestHelper.BuildRepositoryName(source.Name, TestHelper.EnvVar(this.EnvVarPrefix, "Repo"));
            var repo = repoList.Where(r => r.FullName == expectedName).FirstOrDefault();
            Assert.NotNull(repo);
            Assert.True(ValidateUrls(repo));
        }


        [SkippableFact]
        public void GetRepositoryList_NonExistingUser_ThrowsException()
        {
            Skip.If(this.SkipUnauthenticatedTests);

            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "user";
            source.Name = "scm-backup-testuser-does-not-exist";

            List<HosterRepository> repoList;
            Assert.ThrowsAny<Exception>(() => repoList = sut.GetRepositoryList(source));
        }

        [Fact]
        public void GetRepositoryList_AuthenticatedUser_InvalidPasswordThrowsException()
        {
            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "user";
            source.Name = TestHelper.EnvVar(this.EnvVarPrefix, "Name");
            source.AuthName = source.Name;
            source.Password = "invalid-password";

            List<HosterRepository> repoList;
            Assert.ThrowsAny<Exception>(() => repoList = sut.GetRepositoryList(source));
        }

        [Fact]
        public void GetRepositoryList_AuthenticatedUser_Executes()
        {
            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "user";
            source.Name = TestHelper.EnvVar(this.EnvVarPrefix, "Name");
            source.AuthName = source.Name;
            source.Password = TestHelper.EnvVar(this.EnvVarPrefix, "PW");

            var repoList = sut.GetRepositoryList(source);

            // at least one result?
            Assert.NotNull(repoList);
            Assert.True(repoList.Count > 0);

            // specific repo exists?
            string expectedName = TestHelper.BuildRepositoryName(source.Name, TestHelper.EnvVar(this.EnvVarPrefix, "Repo"));
            var repo = repoList.Where(r => r.FullName == expectedName).FirstOrDefault();
            Assert.NotNull(repo);
            Assert.True(ValidateUrls(repo));
        }

        [SkippableFact]
        public void GetRepositoryList_UnauthenticatedOrganization_Executes()
        {
            Skip.If(this.SkipUnauthenticatedTests);

            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "org";
            source.Name = TestHelper.EnvVar(this.EnvVarPrefix, "OrgName");

            var repoList = sut.GetRepositoryList(source);

            // at least one result?
            Assert.NotNull(repoList);
            Assert.True(repoList.Count > 0);

            // specific repo exists?
            string expectedName = TestHelper.BuildRepositoryName(source.Name, TestHelper.EnvVar(this.EnvVarPrefix, "Repo"));
            var repo = repoList.Where(r => r.FullName == expectedName).FirstOrDefault();
            Assert.NotNull(repo);
            Assert.True(ValidateUrls(repo));
        }

        [SkippableFact]
        public void GetRepositoryList_NonExistingOrganization_ThrowsException()
        {
            Skip.If(this.SkipUnauthenticatedTests);

            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "org";
            source.Name = "scm-backup-testorg-does-not-exist";

            List<HosterRepository> repoList;
            Assert.ThrowsAny<Exception>(() => repoList = sut.GetRepositoryList(source));
        }

        [Fact]
        public void GetRepositoryList_AuthenticatedOrganization_Executes()
        {
            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "org";
            source.Name = TestHelper.EnvVar(this.EnvVarPrefix, "OrgName");
            source.AuthName = TestHelper.EnvVar(this.EnvVarPrefix, "Name");
            source.Password = TestHelper.EnvVar(this.EnvVarPrefix, "PW");

            var repoList = sut.GetRepositoryList(source);

            // at least one result?
            Assert.NotNull(repoList);
            Assert.True(repoList.Count > 0);

            // specific repo exists?
            string expectedName = TestHelper.BuildRepositoryName(source.Name, TestHelper.EnvVar(this.EnvVarPrefix, "Repo"));
            var repo = repoList.Where(r => r.FullName == expectedName).FirstOrDefault();
            Assert.NotNull(repo);
            Assert.True(ValidateUrls(repo));
        }

        [SkippableFact]
        public void GetRepositoryList_PrivateRepoIsMarkedAsPrivate()
        {
            string repoName = TestHelper.EnvVar(this.EnvVarPrefix, "RepoPrivate", false);
            Skip.If(repoName == null, "There's no private repo for this hoster");

            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "user";
            source.Name = TestHelper.EnvVar(this.EnvVarPrefix, "Name");
            source.AuthName = source.Name;
            source.Password = TestHelper.EnvVar(this.EnvVarPrefix, "PW");

            var repoList = sut.GetRepositoryList(source);

            // specific repo exists and is private?
            string expectedName = TestHelper.BuildRepositoryName(source.Name, repoName);
            var repo = repoList.Where(r => r.FullName == expectedName).FirstOrDefault();
            Assert.NotNull(repo);
            Assert.True(repo.IsPrivate);
        }

        [Fact]
        public void GetRepositoryList_PaginationWorks()
        {
            var source = new ConfigSource();
            source.Hoster = this.ConfigHoster;
            source.Type = "user";
            source.Name = TestHelper.EnvVar(this.EnvVarPrefix, "PaginationUser");
            source.AuthName = TestHelper.EnvVar(this.EnvVarPrefix, "Name");
            source.Password = TestHelper.EnvVar(this.EnvVarPrefix, "PW");

            var repoList = sut.GetRepositoryList(source);

            Assert.True(repoList.Count > this.Pagination_MinNumberOfRepos);
        }

        private bool ValidateUrls(HosterRepository repo)
        {
            bool result = true;

            var validator = new UrlHelper();
            if (!validator.UrlIsValid(repo.CloneUrl))
            {
                return false;
            }

            if (repo.HasWiki)
            {
                if (!validator.UrlIsValid(repo.WikiUrl))
                {
                    return false;
                }
            }

            if (repo.HasIssues)
            {
                if (!validator.UrlIsValid(repo.IssueUrl))
                {
                    return false;
                }
            }

            return result;
        }
    }
}
