```mermaid

erDiagram
    %% -------------------------
    %% Relationships
    %% -------------------------
    APPLICATION_USER ||--o{ PET : "owns (OwnerId)"
    APPLICATION_USER ||--o{ IMAGE_ASSET : "owns (OwnerId)"

    PET ||--o{ HEALTH_LOG : "has (PetId)"
    PET ||--o{ SCHEDULE_ITEM : "has (PetId)"
    PET ||--o{ VISIT : "has (PetId)"

    HEALTH_LOG ||--o{ HEALTH_LOG_IMAGE : "has (HealthLogId)"
    VISIT ||--o{ VISIT_IMAGE : "has (VisitId)"

    IMAGE_ASSET ||--o{ HEALTH_LOG_IMAGE : "referenced by (ImageId)"
    IMAGE_ASSET ||--o{ VISIT_IMAGE : "referenced by (ImageId)"

    %% Optional single-image references
    APPLICATION_USER ||--o| IMAGE_ASSET : "avatar (AvatarImageId)"
    PET ||--o| IMAGE_ASSET : "photo (PhotoImageId)"

    %% -------------------------
    %% Entities
    %% -------------------------

    %% Identity user (physical table: AspNetUsers)
    APPLICATION_USER {
        string Id PK "AspNetUsers.Id / IdentityUser.Id (default: string)"
        string Email "Identity standard"
        string UserName "Identity standard"

        string DisplayName "Max 50; NOT NULL (default set on save if empty)"
        guid   AvatarImageId FK "nullable -> IMAGE_ASSET.ImageId"
        long   UsedImageBytes "user quota tracking (e.g., <= 100MB)"
        bytes  RowVersion "Timestamp / concurrency token"
    }

    PET {
        int      Id PK
        string   OwnerId FK "NOT NULL -> APPLICATION_USER.Id"
        string   Name              "Max 50"
        string   SpeciesCode       "fixed codes (DOG/CAT/...)"
        string   Breed             "Max 100 (nullable)"
        string   Sex               "Max 10 (nullable)"
        date     BirthDate         "nullable"
        date     AdoptedDate       "nullable"
        boolean  IsPublic          "default true"

        guid     PhotoImageId FK   "nullable -> IMAGE_ASSET.ImageId"
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
    }

    HEALTH_LOG {
        int      Id PK
        int      PetId FK           "-> PET.Id"
        datetimeoffset RecordedAt   "DateTimeOffset (+09:00)"
        double   WeightKg           "0.0 - 200.0 (nullable)"
        int      FoodAmountGram     "0 - 5000 (nullable)"
        int      WalkMinutes        "0 - 1440 (nullable)"
        string   StoolCondition     "Max 50 (nullable)"
        string   Note               "Max 1000 (nullable)"
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
    }

    HEALTH_LOG_IMAGE {
        int    Id PK
        int    HealthLogId FK "-> HEALTH_LOG.Id"
        guid   ImageId FK     "-> IMAGE_ASSET.ImageId"
        int    SortOrder      "0.. (display order)"
    }

    SCHEDULE_ITEM {
        int      Id PK
        int      PetId FK       "-> PET.Id"
        date     DueDate        "required"
        string   Type           "Max 20 (Vaccine/Medicine/Visit/Other)"
        string   Title          "Max 100"
        string   Note           "Max 1000 (nullable)"
        boolean  IsDone         "default false"
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
    }

    VISIT {
        int      Id PK
        int      PetId FK         "-> PET.Id"
        date     VisitDate        "required"
        string   ClinicName       "Max 100 (nullable)"
        string   Diagnosis        "Max 500 (nullable)"
        string   Prescription     "Max 500 (nullable)"
        string   Note             "Max 1000 (nullable)"
        datetimeoffset CreatedAt
        datetimeoffset UpdatedAt
    }

    VISIT_IMAGE {
        int    Id PK
        int    VisitId FK  "-> VISIT.Id"
        guid   ImageId FK  "-> IMAGE_ASSET.ImageId"
        int    SortOrder   "0.. (display order)"
    }

    IMAGE_ASSET {
        guid     ImageId PK
        string   StorageKey       "e.g., images/{guid}.jpg"
        string   ContentType      "image/jpeg|png|webp"
        long     SizeBytes
        string   OwnerId FK       "-> APPLICATION_USER.Id"
        string   Category         "Avatar/PetPhoto/HealthLog/Visit"
        string   Status           "Pending/Ready (default: Pending)"
        datetimeoffset CreatedAt
    }

```

---

# ER図補足（インデックス／制約／FK／削除ルール）

## インデックス

- **PET**
  - `IX_PET_OwnerId`：`(OwnerId)`
  - `IX_PET_IsPublic_SpeciesCode`：`(IsPublic, SpeciesCode)`
  - `IX_PET_Name`：`(Name)`

- **HEALTH_LOG**
  - `IX_HEALTH_LOG_PetId_RecordedAt`：`(PetId, RecordedAt DESC)`

- **SCHEDULE_ITEM**
  - `IX_SCHEDULE_ITEM_PetId_DueDate`：`(PetId, DueDate ASC)`
  - （必要なら）`IX_SCHEDULE_ITEM_PetId_IsDone_DueDate`：`(PetId, IsDone, DueDate ASC)`

- **VISIT**
  - `IX_VISIT_PetId_VisitDate`：`(PetId, VisitDate DESC)`

- **IMAGE_ASSET**
  - `IX_IMAGE_ASSET_OwnerId_Status`：`(OwnerId, Status)`
  - `IX_IMAGE_ASSET_CreatedAt`：`(CreatedAt)`
  - `IX_IMAGE_ASSET_StorageKey`：`(StorageKey)`

- **HEALTH_LOG_IMAGE**
  - `IX_HEALTH_LOG_IMAGE_HealthLogId_SortOrder`：`(HealthLogId, SortOrder)`
  - `IX_HEALTH_LOG_IMAGE_ImageId`：`(ImageId)`

- **VISIT_IMAGE**
  - `IX_VISIT_IMAGE_VisitId_SortOrder`：`(VisitId, SortOrder)`
  - `IX_VISIT_IMAGE_ImageId`：`(ImageId)`

---

## 一意制約

- **HEALTH_LOG_IMAGE**
  - `UQ_HEALTH_LOG_IMAGE_HealthLogId_SortOrder`：`(HealthLogId, SortOrder)`  
    - 画像の「表示順」を安定させ、同一ログ内の重複順序を防ぐ

- **VISIT_IMAGE**
  - `UQ_VISIT_IMAGE_VisitId_SortOrder`：`(VisitId, SortOrder)`  
    - 同上（診療記録の添付画像）

- **IMAGE_ASSET**
  - `UQ_IMAGE_ASSET_StorageKey`：`(StorageKey)`  

---

## 外部キー制約（一覧）

- `PET.OwnerId -> APPLICATION_USER.Id`
- `HEALTH_LOG.PetId -> PET.Id`
- `SCHEDULE_ITEM.PetId -> PET.Id`
- `VISIT.PetId -> PET.Id`
- `HEALTH_LOG_IMAGE.HealthLogId -> HEALTH_LOG.Id`
- `HEALTH_LOG_IMAGE.ImageId -> IMAGE_ASSET.ImageId`
- `VISIT_IMAGE.VisitId -> VISIT.Id`
- `VISIT_IMAGE.ImageId -> IMAGE_ASSET.ImageId`
- `IMAGE_ASSET.OwnerId -> APPLICATION_USER.Id`
- `APPLICATION_USER.AvatarImageId -> IMAGE_ASSET.ImageId`（nullable）
- `PET.PhotoImageId -> IMAGE_ASSET.ImageId`（nullable）

---

## 削除ルール

- `PET` を削除する場合：関連する `HEALTH_LOG / SCHEDULE_ITEM / VISIT` を **アプリ側で先に削除**
- `HEALTH_LOG` 削除：`HEALTH_LOG_IMAGE` を **アプリ側で先に削除**
- `VISIT` 削除：`VISIT_IMAGE` を **アプリ側で先に削除**
