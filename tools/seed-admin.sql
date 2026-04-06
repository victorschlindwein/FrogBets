INSERT INTO "Users" ("Id","Username","PasswordHash","IsAdmin","VirtualBalance","ReservedBalance","WinsCount","LossesCount","CreatedAt")
VALUES (gen_random_uuid(), 'admin', '$2a$11$ylCvlSmwe8OVZyDGa0yX/ucu5/YZpViYoEdYovhXxN5D/P/vXxnTi', true, 1000, 0, 0, 0, NOW())
ON CONFLICT ("Username") DO UPDATE SET "PasswordHash" = '$2a$11$ylCvlSmwe8OVZyDGa0yX/ucu5/YZpViYoEdYovhXxN5D/P/vXxnTi', "IsAdmin" = true;
