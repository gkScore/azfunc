drop table if exists reputations;

create table reputations (
    evaluatee_address NVARCHAR(100) PRIMARY KEY,
    score FLOAT NOT NULL,
    reputation_count INT NOT NULL,
    update_time DATETIME NOT NULL DEFAULT GETUTCDATE()
);

drop table if exists reputation_transactions;

create table reputation_transactions (
    id BIGINT PRIMARY KEY IDENTITY(1, 1),
    reviewer_address NVARCHAR(100) NOT NULL,
    evaluatee_address NVARCHAR(100) NOT NULL,
    score FLOAT NOT NULL,
    evaluated_time DATETIME NOT NULL DEFAULT GETUTCDATE()
);
