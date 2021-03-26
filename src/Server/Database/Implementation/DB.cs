// MIT License - Copyright (C) ryancheung and the FelCore team
// This file is subject to the terms and conditions defined in
// file 'LICENSE', which is part of this source code package.

using System;
using Common;

namespace Server.Database
{
    public static class DB
    {
        public static readonly DatabaseWorkerPool<LoginDatabaseConnection, LoginStatements> LoginDatabase
            = new DatabaseWorkerPool<LoginDatabaseConnection, LoginStatements>();
    }
}
