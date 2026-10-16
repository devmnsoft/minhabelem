-- MINHABELEM.SQL - fonte unica do esquema MinhaBelem
-- MySQL 8.0+. Selecione a base do projeto no SGBD e execute TODO este arquivo.
-- O banco deve existir: nao exige CREATE DATABASE, CREATE USER ou privilegios administrativos.
-- Exemplo no cliente mysql: mysql -h HOST -u USUARIO -p NOME_DA_BASE < MINHABELEM.SQL
-- Preserva tabelas e dados existentes. CREATE TABLE IF NOT EXISTS nao atualiza colunas antigas.
-- Inclui tabelas do portal atual e tabelas mb_ da infraestrutura em integracao.
-- Nao copia automaticamente dados entre essas duas estruturas e nao cria usuario/senha de acesso.
-- DDL no MySQL tem commit implicito; uma falha pode deixar parte das tabelas criada.
-- Corrija a causa e reexecute o arquivo. Seeds nao sobrescrevem papeis/permissoes existentes.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS schema_migrations (
    version VARCHAR(150) NOT NULL PRIMARY KEY,
    checksum CHAR(64) NOT NULL,
    applied_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    duration_ms INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- PARTE 1: tabelas usadas pelo portal atual.
-- MinhaBelém - MySQL 8.0+, executar dentro da base já criada pela Locaweb.
-- Não utiliza CREATE DATABASE ou CREATE USER (permissões restritas em hospedagem compartilhada).
CREATE TABLE IF NOT EXISTS editorial_users (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 email VARCHAR(254) NOT NULL UNIQUE,
 password_hash VARCHAR(500) NOT NULL,
 display_name VARCHAR(180) NOT NULL,
 role ENUM('superadmin','editor','moderator','advertiser') NOT NULL,
 enabled BOOLEAN NOT NULL DEFAULT TRUE,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS ingestion_sources (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, source_name VARCHAR(200) NOT NULL,
 feed_url VARCHAR(1500) NOT NULL, feed_url_hash BINARY(32) NOT NULL UNIQUE,
 protocol ENUM('rss','atom','gtfs','gtfs-rt','api') NOT NULL DEFAULT 'rss',
 enabled BOOLEAN NOT NULL DEFAULT FALSE, approved BOOLEAN NOT NULL DEFAULT FALSE,
 last_checked_at DATETIME(6), last_error TEXT, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS ingestion_candidates (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, source_id BIGINT NOT NULL,
 title VARCHAR(300) NOT NULL, summary VARCHAR(1500) NOT NULL DEFAULT '',
 source_url VARCHAR(2000) NOT NULL, source_url_hash BINARY(32) NOT NULL UNIQUE,
 published_at DATETIME(6) NOT NULL, status ENUM('pending','rejected','approved') NOT NULL DEFAULT 'pending',
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY (source_id) REFERENCES ingestion_sources(id),
 INDEX idx_candidates_status (status,created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS content_items (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, category VARCHAR(60) NOT NULL,
 title VARCHAR(300) NOT NULL, summary VARCHAR(2500) NOT NULL DEFAULT '',
 neighborhood VARCHAR(120), source_name VARCHAR(240) NOT NULL,
 source_url VARCHAR(2000) NOT NULL, source_url_hash BINARY(32) NOT NULL UNIQUE,
 author_name VARCHAR(240), image_url VARCHAR(2000), image_credit VARCHAR(300),
 image_license VARCHAR(120), address VARCHAR(500), event_at DATETIME(6),
 end_at DATETIME(6), ticket_price DECIMAL(12,2),
 published_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 last_verified_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 verification_status ENUM('pending','verified','outdated') NOT NULL DEFAULT 'pending',
 status ENUM('draft','review','published','archived') NOT NULL DEFAULT 'draft',
 reviewed_by BIGINT NULL, created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY (reviewed_by) REFERENCES editorial_users(id),
 INDEX idx_content_public (status,category,published_at),
 CHECK(ticket_price IS NULL OR ticket_price >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS community_submissions (
 id BIGINT AUTO_INCREMENT PRIMARY KEY, display_name VARCHAR(100) NOT NULL,
 message VARCHAR(2500) NOT NULL,status ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 reviewed_by BIGINT NULL,FOREIGN KEY(reviewed_by) REFERENCES editorial_users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS transport_lines (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,agency VARCHAR(160) NOT NULL,
 line_code VARCHAR(50) NOT NULL,description TEXT,source_url VARCHAR(2000),
 last_verified_at DATETIME(6),UNIQUE KEY unique_line (agency,line_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS transport_stops (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,agency VARCHAR(160) NOT NULL,stop_code VARCHAR(150) NOT NULL,
 name VARCHAR(250) NOT NULL,latitude DECIMAL(10,7),longitude DECIMAL(10,7),
 UNIQUE KEY unique_stop (agency,stop_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS transport_alerts (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,title VARCHAR(300) NOT NULL,description TEXT NOT NULL,
 source_url VARCHAR(2000) NOT NULL,valid_from DATETIME(6) NOT NULL,valid_to DATETIME(6),
 verified BOOLEAN NOT NULL DEFAULT FALSE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS advertiser_campaigns (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,advertiser_name VARCHAR(250) NOT NULL,
 destination_url VARCHAR(2000) NOT NULL,image_url VARCHAR(2000),
 label VARCHAR(100) NOT NULL DEFAULT 'Publicidade',active BOOLEAN NOT NULL DEFAULT FALSE,
 valid_from DATETIME(6),valid_to DATETIME(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS ai_review_jobs (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,provider VARCHAR(60) NOT NULL,
 candidate_id BIGINT, status VARCHAR(30) NOT NULL DEFAULT 'pending',
 output_text LONGTEXT,created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY(candidate_id) REFERENCES ingestion_candidates(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS audit_logs (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,actor_id BIGINT NULL,action VARCHAR(140) NOT NULL,
 entity_type VARCHAR(140) NOT NULL,entity_id BIGINT,details JSON,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 FOREIGN KEY(actor_id) REFERENCES editorial_users(id),
 INDEX idx_audit_date (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- PARTE 2: infraestrutura da Sprint 02.

-- 001_identidade.sql
-- Identidade ASP.NET Core (tabelas próprias, mantidas por Dapper) + permissões por funcionalidade.

CREATE TABLE IF NOT EXISTS mb_users (
    id                       BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_name                VARCHAR(256) NULL,
    normalized_user_name     VARCHAR(256) NULL,
    email                    VARCHAR(256) NULL,
    normalized_email         VARCHAR(256) NULL,
    email_confirmed          TINYINT(1)     NOT NULL DEFAULT 0,
    password_hash            VARCHAR(256) NULL,
    security_stamp           VARCHAR(32)  NULL,
    concurrency_stamp        VARCHAR(32)  NULL,
    lockout_enabled          TINYINT(1)     NOT NULL DEFAULT 1,
    lockout_end              DATETIME(6) NULL,
    access_failed_count      INT            NOT NULL DEFAULT 0,
    display_name             VARCHAR(180) NOT NULL DEFAULT '',
    enabled                  TINYINT(1)     NOT NULL DEFAULT 1,
    mfa_enabled              TINYINT(1)     NOT NULL DEFAULT 0,
    phone_number             VARCHAR(32)  NULL,
    phone_number_confirmed   TINYINT(1)     NOT NULL DEFAULT 0,
    two_factor_enabled       TINYINT(1)     NOT NULL DEFAULT 0,
    lockout_date             DATETIME(6) NULL,
    password_changed_at      DATETIME(6) NULL,
    last_login_at            DATETIME(6) NULL,
    created_at               DATETIME(6)    NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_users_normalized_name (normalized_user_name),
    UNIQUE KEY uq_users_normalized_email (normalized_email),
    KEY idx_users_display (display_name)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_roles (
    id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name               VARCHAR(256) NOT NULL,
    normalized_name    VARCHAR(256) NOT NULL,
    concurrency_stamp  VARCHAR(32)  NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_roles_normalized (normalized_name)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_roles (
    user_id   BIGINT UNSIGNED NOT NULL,
    role_id   BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (user_id, role_id),
    KEY idx_user_roles_role (role_id),
    CONSTRAINT fk_user_roles_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE,
    CONSTRAINT fk_user_roles_role FOREIGN KEY (role_id) REFERENCES mb_roles (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_claims (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id      BIGINT UNSIGNED NOT NULL,
    claim_type   VARCHAR(256) NOT NULL,
    claim_value  VARCHAR(1024) NULL,
    PRIMARY KEY (id),
    KEY idx_user_claims_user (user_id, claim_type),
    CONSTRAINT fk_user_claims_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_role_claims (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    role_id      BIGINT UNSIGNED NOT NULL,
    claim_type   VARCHAR(256) NOT NULL,
    claim_value  VARCHAR(1024) NULL,
    PRIMARY KEY (id),
    KEY idx_role_claims_role (role_id, claim_type),
    CONSTRAINT fk_role_claims_role FOREIGN KEY (role_id) REFERENCES mb_roles (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_logins (
    login_provider          VARCHAR(128) NOT NULL,
    provider_key            VARCHAR(256) NOT NULL,
    provider_display_name   VARCHAR(256) NULL,
    user_id                 BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (login_provider, provider_key),
    KEY idx_user_logins_user (user_id),
    CONSTRAINT fk_user_logins_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_user_tokens (
    user_id          BIGINT UNSIGNED NOT NULL,
    login_provider   VARCHAR(128) NOT NULL,
    name             VARCHAR(128) NOT NULL,
    value_cipher     VARCHAR(2048) NOT NULL,
    PRIMARY KEY (user_id, login_provider, name),
    CONSTRAINT fk_user_tokens_user FOREIGN KEY (user_id) REFERENCES mb_users (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_permissions (
    code        VARCHAR(64)  NOT NULL,
    description VARCHAR(255) NOT NULL,
    PRIMARY KEY (code)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_role_permissions (
    role_id         BIGINT UNSIGNED NOT NULL,
    permission_code VARCHAR(64)     NOT NULL,
    PRIMARY KEY (role_id, permission_code),
    KEY idx_role_permissions_perm (permission_code),
    CONSTRAINT fk_role_perm_role FOREIGN KEY (role_id) REFERENCES mb_roles (id) ON DELETE CASCADE,
    CONSTRAINT fk_role_perm_perm FOREIGN KEY (permission_code) REFERENCES mb_permissions (code) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_audit_logs (
    id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    actor_id     BIGINT UNSIGNED NULL,
    actor_name   VARCHAR(180) NULL,
    action       VARCHAR(140) NOT NULL,
    entity_type  VARCHAR(140) NOT NULL,
    entity_id    BIGINT UNSIGNED NULL,
    details      JSON NULL,
    ip_address   VARCHAR(45)  NULL,
    created_at   DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_audit_created (created_at),
    KEY idx_audit_entity (entity_type, entity_id),
    KEY idx_audit_actor (actor_id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 002_conteudo.sql
-- Conteúdo editorial, revisões, públicos e recursos de acessibilidade.

CREATE TABLE IF NOT EXISTS mb_content_items (
    id                     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    kind                   VARCHAR(24)  NOT NULL,
    category               VARCHAR(32)  NOT NULL,
    subject                VARCHAR(64)  NULL,
    title                  VARCHAR(300) NOT NULL,
    slug                   VARCHAR(180) NOT NULL,
    summary                VARCHAR(2500) NOT NULL DEFAULT '',
    body                   MEDIUMTEXT NULL,
    neighborhood           VARCHAR(120) NULL,
    municipality           VARCHAR(120) NULL,
    state                  CHAR(2)      NOT NULL DEFAULT 'PA',
    address                VARCHAR(500) NULL,
    latitude               DECIMAL(10, 7) NULL,
    longitude              DECIMAL(10, 7) NULL,
    source_id              BIGINT UNSIGNED NULL,
    source_name            VARCHAR(240) NOT NULL,
    source_url             VARCHAR(2000) NOT NULL,
    source_url_hash        BINARY(32)   NOT NULL,
    fingerprint            CHAR(64)     NOT NULL,
    author_name            VARCHAR(240) NULL,
    source_published_at    DATETIME(6) NULL,
    image_url              VARCHAR(2000) NULL,
    image_credit           VARCHAR(300) NULL,
    image_license          VARCHAR(120) NULL,
    event_start_at         DATETIME(6) NULL,
    event_end_at           DATETIME(6) NULL,
    ticket_price           DECIMAL(12, 2) NULL,
    ticket_price_text      VARCHAR(120) NULL,
    is_free                TINYINT(1)   NOT NULL DEFAULT 0,
    status                 VARCHAR(24)  NOT NULL DEFAULT 'Draft',
    verification_status    VARCHAR(24)  NOT NULL DEFAULT 'Pending',
    flags                  INT          NOT NULL DEFAULT 0,
    duplicate_of_id        BIGINT UNSIGNED NULL,
    requires_human_approval TINYINT(1)  NOT NULL DEFAULT 1,
    published_at           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    expires_at             DATETIME(6) NULL,
    last_verified_at       DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    reviewed_by_id         BIGINT UNSIGNED NULL,
    created_by_id          BIGINT UNSIGNED NULL,
    revision               INT          NOT NULL DEFAULT 1,
    created_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_content_slug (slug),
    UNIQUE KEY uq_content_source_url (source_url_hash),
    KEY idx_content_public (status, published_at),
    KEY idx_content_category (category, status, published_at),
    KEY idx_content_event (status, event_start_at),
    KEY idx_content_neighborhood (neighborhood),
    KEY idx_content_municipality (municipality),
    KEY idx_content_status_updated (status, updated_at),
    KEY idx_content_fingerprint (fingerprint),
    CONSTRAINT fk_content_duplicate FOREIGN KEY (duplicate_of_id) REFERENCES mb_content_items (id) ON DELETE SET NULL,
    CONSTRAINT chk_content_price CHECK (ticket_price IS NULL OR ticket_price >= 0)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_content_audiences (
    content_id   BIGINT UNSIGNED NOT NULL,
    audience     VARCHAR(24)  NOT NULL,
    PRIMARY KEY (content_id, audience),
    CONSTRAINT fk_content_aud_content FOREIGN KEY (content_id) REFERENCES mb_content_items (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_content_accessibility (
    content_id   BIGINT UNSIGNED NOT NULL,
    feature      VARCHAR(32)  NOT NULL,
    PRIMARY KEY (content_id, feature),
    CONSTRAINT fk_content_acc_content FOREIGN KEY (content_id) REFERENCES mb_content_items (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_content_revisions (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    content_id      BIGINT UNSIGNED NOT NULL,
    revision        INT          NOT NULL,
    change_summary  VARCHAR(500) NULL,
    snapshot        JSON         NOT NULL,
    changed_by_id   BIGINT UNSIGNED NULL,
    changed_by_name VARCHAR(180) NULL,
    changed_at      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_content_revision (content_id, revision),
    CONSTRAINT fk_revisions_content FOREIGN KEY (content_id) REFERENCES mb_content_items (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 003_coleta.sql
-- Fontes de coleta, execuções (histórico) e candidatos.

CREATE TABLE IF NOT EXISTS mb_ingestion_sources (
    id                     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name                   VARCHAR(200) NOT NULL,
    feed_url               VARCHAR(2000) NOT NULL,
    feed_url_hash          BINARY(32)   NOT NULL,
    protocol               VARCHAR(16)  NOT NULL DEFAULT 'Rss',
    topics                 VARCHAR(255) NULL,
    municipality           VARCHAR(120) NULL,
    enabled                TINYINT(1)   NOT NULL DEFAULT 0,
    approved               TINYINT(1)   NOT NULL DEFAULT 0,
    respect_robots         TINYINT(1)   NOT NULL DEFAULT 1,
    license_note           VARCHAR(500) NULL,
    last_checked_at        DATETIME(6) NULL,
    last_success_at        DATETIME(6) NULL,
    last_error             TEXT NULL,
    consecutive_failures   INT          NOT NULL DEFAULT 0,
    etag                   VARCHAR(200) NULL,
    last_modified          DATETIME(6) NULL,
    created_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_source_url (feed_url_hash),
    KEY idx_source_operational (enabled, approved)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_ingestion_runs (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    started_at      DATETIME(6) NOT NULL,
    finished_at     DATETIME(6) NOT NULL,
    status          VARCHAR(16)  NOT NULL,
    sources_checked INT          NOT NULL DEFAULT 0,
    sources_failed  INT          NOT NULL DEFAULT 0,
    items_seen      INT          NOT NULL DEFAULT 0,
    items_inserted  INT          NOT NULL DEFAULT 0,
    items_duplicate INT          NOT NULL DEFAULT 0,
    error           TEXT NULL,
    PRIMARY KEY (id),
    KEY idx_runs_started (started_at)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_ingestion_candidates (
    id                BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    source_id         BIGINT UNSIGNED NOT NULL,
    source_name       VARCHAR(200) NOT NULL,
    run_id            BIGINT UNSIGNED NULL,
    external_id       VARCHAR(300) NULL,
    title             VARCHAR(300) NOT NULL,
    summary           VARCHAR(1500) NOT NULL DEFAULT '',
    source_url        VARCHAR(2000) NOT NULL,
    source_url_hash   BINARY(32)   NOT NULL,
    fingerprint       CHAR(64)     NOT NULL,
    author_name       VARCHAR(240) NULL,
    published_at      DATETIME(6)  NOT NULL,
    image_url         VARCHAR(2000) NULL,
    image_credit      VARCHAR(300) NULL,
    status            VARCHAR(16)  NOT NULL DEFAULT 'Pending',
    duplicate_of_id   BIGINT UNSIGNED NULL,
    content_id        BIGINT UNSIGNED NULL,
    review_notes      VARCHAR(1000) NULL,
    created_at        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at        DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_candidate_url (source_url_hash),
    KEY idx_candidate_status (status, created_at),
    KEY idx_candidate_source (source_id),
    KEY idx_candidate_fingerprint (fingerprint),
    CONSTRAINT fk_candidate_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources (id) ON DELETE CASCADE,
    CONSTRAINT fk_candidate_run FOREIGN KEY (run_id) REFERENCES mb_ingestion_runs (id) ON DELETE SET NULL,
    CONSTRAINT fk_candidate_duplicate FOREIGN KEY (duplicate_of_id) REFERENCES mb_ingestion_candidates (id) ON DELETE SET NULL
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 004_ia.sql
-- Registro obrigatório de cada execução de IA: provedor, modelo, versão, custo, latência e resultado.

CREATE TABLE IF NOT EXISTS mb_ai_runs (
    id                   BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    provider             VARCHAR(24)  NOT NULL,
    model                VARCHAR(120) NOT NULL,
    model_version        VARCHAR(120) NOT NULL DEFAULT '',
    task                 VARCHAR(32)  NOT NULL,
    target_type          VARCHAR(40)  NOT NULL,
    target_id            BIGINT UNSIGNED NULL,
    prompt_chars         INT          NOT NULL DEFAULT 0,
    prompt_tokens        INT          NULL,
    completion_tokens    INT          NULL,
    cost_usd             DECIMAL(12, 6) NOT NULL DEFAULT 0,
    latency_ms           INT          NOT NULL DEFAULT 0,
    success              TINYINT(1)   NOT NULL DEFAULT 0,
    error                TEXT NULL,
    response_json        JSON NULL,
    triggered_by_user_id BIGINT UNSIGNED NULL,
    created_at           DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_ai_created (created_at),
    KEY idx_ai_task (task, created_at),
    KEY idx_ai_provider (provider, created_at)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 005_guia.sql
-- Lugares do guia (Google Places). O place_id é armazenado de forma permanente; o restante
-- tem prazo de expurgo para atender à política de armazenamento de conteúdo do Google Places.

CREATE TABLE IF NOT EXISTS mb_places (
    id                     BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    google_place_id        VARCHAR(255) NOT NULL,
    slug                   VARCHAR(180) NOT NULL,
    name                   VARCHAR(300) NOT NULL,
    formatted_address      VARCHAR(500) NULL,
    neighborhood           VARCHAR(120) NULL,
    municipality           VARCHAR(120) NULL,
    state                  CHAR(2)      NOT NULL DEFAULT 'PA',
    latitude               DECIMAL(10, 7) NULL,
    longitude              DECIMAL(10, 7) NULL,
    google_maps_uri        VARCHAR(2000) NULL,
    website_uri            VARCHAR(2000) NULL,
    phone                  VARCHAR(64)  NULL,
    primary_type           VARCHAR(80)  NULL,
    types                  JSON NULL,
    rating                 DECIMAL(3, 2) NULL,
    rating_count           INT NULL,
    business_status        VARCHAR(32)  NULL,
    opening_hours          JSON NULL,
    price_level            TINYINT NULL,
    price_text             VARCHAR(120) NULL,
    wheelchair_accessible TINYINT(1) NULL,
    photo_name             VARCHAR(255) NULL,
    photo_attribution      VARCHAR(500) NULL,
    is_published           TINYINT(1)   NOT NULL DEFAULT 1,
    editorial_note         VARCHAR(1000) NULL,
    editorial_note_source  VARCHAR(2000) NULL,
    cached_at              DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    purge_after            DATETIME(6)  NOT NULL,
    created_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at             DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_place_google (google_place_id),
    UNIQUE KEY uq_place_slug (slug),
    KEY idx_place_geo (latitude, longitude),
    KEY idx_place_neighborhood (neighborhood),
    KEY idx_place_municipality (municipality),
    KEY idx_place_type (primary_type),
    KEY idx_place_purge (purge_after),
    FULLTEXT KEY ft_place_name (name, formatted_address)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_place_audiences (
    place_id   BIGINT UNSIGNED NOT NULL,
    audience   VARCHAR(24)  NOT NULL,
    PRIMARY KEY (place_id, audience),
    CONSTRAINT fk_place_aud_place FOREIGN KEY (place_id) REFERENCES mb_places (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_place_accessibility (
    place_id   BIGINT UNSIGNED NOT NULL,
    feature    VARCHAR(32)  NOT NULL,
    PRIMARY KEY (place_id, feature),
    CONSTRAINT fk_place_acc_place FOREIGN KEY (place_id) REFERENCES mb_places (id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- 006_plataforma.sql
CREATE TABLE IF NOT EXISTS mb_community_submissions (
    id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    display_name    VARCHAR(100) NOT NULL,
    contact         VARCHAR(160) NULL,
    message         VARCHAR(2500) NOT NULL,
    status          VARCHAR(16)  NOT NULL DEFAULT 'Pending',
    review_notes    VARCHAR(1000) NULL,
    reviewed_by_id  BIGINT UNSIGNED NULL,
    created_at      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at      DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_submissions_status (status, created_at)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_advertiser_campaigns (
    id                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    advertiser_name    VARCHAR(250) NOT NULL,
    destination_url    VARCHAR(2000) NOT NULL,
    image_url          VARCHAR(2000) NULL,
    label              VARCHAR(100) NOT NULL DEFAULT 'Publicidade',
    headline           VARCHAR(200) NULL,
    active             TINYINT(1)   NOT NULL DEFAULT 0,
    valid_from         DATETIME(6) NULL,
    valid_to           DATETIME(6) NULL,
    advertiser_user_id VARCHAR(256) NULL,
    created_at         DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    KEY idx_campaigns_active (active, valid_from, valid_to)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_localities (
    id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name        VARCHAR(120) NOT NULL,
    slug        VARCHAR(140) NOT NULL,
    kind        VARCHAR(16)  NOT NULL,
    state       CHAR(2)      NOT NULL DEFAULT 'PA',
    active      TINYINT(1)   NOT NULL DEFAULT 1,
    PRIMARY KEY (id),
    UNIQUE KEY uq_locality (slug, kind)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS mb_settings (
    setting_key   VARCHAR(80)  NOT NULL,
    setting_value TEXT NULL,
    updated_at    DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (setting_key)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- PARTE 3: papeis e permissoes, sem IDs fixos.
INSERT IGNORE INTO mb_permissions (code, description) VALUES
 ('dashboard.read',      'Ver painel com indicadores reais'),
 ('content.read',        'Ver conteúdo em qualquer estado'),
 ('content.write',       'Criar e editar conteúdo'),
 ('content.publish',     'Publicar, despublicar e arquivar'),
 ('content.delete',      'Excluir conteúdo definitivamente'),
 ('candidate.read',      'Ver candidatos coletados'),
 ('candidate.review',    'Aprovar ou rejeitar candidatos'),
 ('source.manage',       'Cadastrar e ativar fontes de coleta'),
 ('place.manage',        'Curar lugares do guia'),
 ('submission.moderate', 'Moderar colaborações da comunidade'),
 ('advertiser.manage',   'Gerenciar campanhas de anunciantes'),
 ('ai.run',              'Executar análise de inteligência artificial'),
 ('audit.read',          'Consultar trilha de auditoria'),
 ('user.manage',         'Gerenciar usuários, papéis e permissões'),
 ('settings.manage',     'Alterar configurações da plataforma');

INSERT IGNORE INTO mb_roles (name, normalized_name, concurrency_stamp) VALUES
 ('SuperAdmin', 'SUPERADMIN', 'seed0000000000000000000000000001'),
 ('Editor', 'EDITOR', 'seed0000000000000000000000000002'),
 ('Moderator', 'MODERATOR', 'seed0000000000000000000000000003'),
 ('Advertiser', 'ADVERTISER', 'seed0000000000000000000000000004');
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'dashboard.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.write' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.publish' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.delete' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.review' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'source.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'place.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'submission.moderate' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'advertiser.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'ai.run' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'audit.read' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'user.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'settings.manage' FROM mb_roles WHERE normalized_name = 'SUPERADMIN';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'dashboard.read' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.read' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.write' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.publish' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.read' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.review' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'source.manage' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'place.manage' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'submission.moderate' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'ai.run' FROM mb_roles WHERE normalized_name = 'EDITOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'dashboard.read' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'content.read' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.read' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'candidate.review' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'place.manage' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'submission.moderate' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'ai.run' FROM mb_roles WHERE normalized_name = 'MODERATOR';
INSERT IGNORE INTO mb_role_permissions (role_id, permission_code)
 SELECT id, 'advertiser.manage' FROM mb_roles WHERE normalized_name = 'ADVERTISER';

-- Fim do script. Fontes e usuarios devem ser cadastrados pela aplicacao.

-- @migration 003_operacao
-- Sprint 03: incremento aditivo. Preserva dados e tabelas anteriores.
CREATE TABLE IF NOT EXISTS mb_user_sessions (
    id CHAR(32) NOT NULL PRIMARY KEY,
    user_id BIGINT UNSIGNED NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    expires_at DATETIME(6) NOT NULL,
    revoked_at DATETIME(6) NULL,
    KEY idx_session_user (user_id, expires_at),
    CONSTRAINT fk_session_user FOREIGN KEY (user_id) REFERENCES mb_users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- @migration 004_descoberta
-- Sprint 03B: configuracao, limites e evidencias de descoberta.
CREATE TABLE IF NOT EXISTS mb_source_profiles (
    source_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    owner_name VARCHAR(180) NOT NULL DEFAULT '',
    organization VARCHAR(200) NOT NULL DEFAULT '',
    region VARCHAR(120) NOT NULL DEFAULT 'Pará',
    keywords VARCHAR(500) NOT NULL DEFAULT '',
    frequency_minutes INT NOT NULL DEFAULT 10,
    daily_request_limit INT NOT NULL DEFAULT 24,
    max_items INT NOT NULL DEFAULT 20,
    credential_reference VARCHAR(80) NULL,
    checkpoint VARCHAR(1000) NULL,
    status VARCHAR(40) NOT NULL DEFAULT 'AwaitingConfiguration',
    next_due_at DATETIME(6) NULL,
    records_found BIGINT NOT NULL DEFAULT 0,
    CONSTRAINT fk_profile_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources(id) ON DELETE CASCADE,
    CONSTRAINT chk_source_frequency CHECK (frequency_minutes BETWEEN 10 AND 1440),
    CONSTRAINT chk_source_requests CHECK (daily_request_limit BETWEEN 1 AND 1000)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_source_requests (
    source_id BIGINT UNSIGNED NOT NULL,
    day DATE NOT NULL,
    requests INT NOT NULL DEFAULT 0,
    PRIMARY KEY(source_id,day),
    CONSTRAINT fk_request_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_discovery_versions (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    candidate_id BIGINT UNSIGNED NOT NULL,
    fingerprint CHAR(64) NOT NULL,
    snapshot JSON NOT NULL,
    discovered_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    UNIQUE KEY uq_discovery_version(candidate_id,fingerprint),
    CONSTRAINT fk_discovery_candidate FOREIGN KEY(candidate_id) REFERENCES mb_ingestion_candidates(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_event_evidence (
    content_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    evidence JSON NOT NULL,
    CONSTRAINT fk_event_evidence FOREIGN KEY(content_id) REFERENCES mb_content_items(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_api_usage (
    service VARCHAR(64) NOT NULL,
    day DATE NOT NULL,
    requests INT NOT NULL DEFAULT 0,
    PRIMARY KEY(service,day)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- @migration 005_localidades
-- Municipios: API IBGE UF 15, consulta 16/10/2026. Bairros: catalogo CODEM de referencia.
INSERT IGNORE INTO mb_localities (name,slug,kind) VALUES
('Abaetetuba','abaetetuba','municipality'),
('Abel Figueiredo','abel-figueiredo','municipality'),
('Acará','acara','municipality'),
('Afuá','afua','municipality'),
('Água Azul do Norte','agua-azul-do-norte','municipality'),
('Alenquer','alenquer','municipality'),
('Almeirim','almeirim','municipality'),
('Altamira','altamira','municipality'),
('Anajás','anajas','municipality'),
('Ananindeua','ananindeua','municipality'),
('Anapu','anapu','municipality'),
('Augusto Corrêa','augusto-correa','municipality'),
('Aurora do Pará','aurora-do-para','municipality'),
('Aveiro','aveiro','municipality'),
('Bagre','bagre','municipality'),
('Baião','baiao','municipality'),
('Bannach','bannach','municipality'),
('Barcarena','barcarena','municipality'),
('Belém','belem','municipality'),
('Belterra','belterra','municipality'),
('Benevides','benevides','municipality'),
('Bom Jesus do Tocantins','bom-jesus-do-tocantins','municipality'),
('Bonito','bonito','municipality'),
('Bragança','braganca','municipality'),
('Brasil Novo','brasil-novo','municipality'),
('Brejo Grande do Araguaia','brejo-grande-do-araguaia','municipality'),
('Breu Branco','breu-branco','municipality'),
('Breves','breves','municipality'),
('Bujaru','bujaru','municipality'),
('Cachoeira do Arari','cachoeira-do-arari','municipality'),
('Cachoeira do Piriá','cachoeira-do-piria','municipality'),
('Cametá','cameta','municipality'),
('Canaã dos Carajás','canaa-dos-carajas','municipality'),
('Capanema','capanema','municipality'),
('Capitão Poço','capitao-poco','municipality'),
('Castanhal','castanhal','municipality'),
('Chaves','chaves','municipality'),
('Colares','colares','municipality'),
('Conceição do Araguaia','conceicao-do-araguaia','municipality'),
('Concórdia do Pará','concordia-do-para','municipality'),
('Cumaru do Norte','cumaru-do-norte','municipality'),
('Curionópolis','curionopolis','municipality'),
('Curralinho','curralinho','municipality'),
('Curuá','curua','municipality'),
('Curuçá','curuca','municipality'),
('Dom Eliseu','dom-eliseu','municipality'),
('Eldorado do Carajás','eldorado-do-carajas','municipality'),
('Faro','faro','municipality'),
('Floresta do Araguaia','floresta-do-araguaia','municipality'),
('Garrafão do Norte','garrafao-do-norte','municipality'),
('Goianésia do Pará','goianesia-do-para','municipality'),
('Gurupá','gurupa','municipality'),
('Igarapé-Açu','igarape-acu','municipality'),
('Igarapé-Miri','igarape-miri','municipality'),
('Inhangapi','inhangapi','municipality'),
('Ipixuna do Pará','ipixuna-do-para','municipality'),
('Irituia','irituia','municipality'),
('Itaituba','itaituba','municipality'),
('Itupiranga','itupiranga','municipality'),
('Jacareacanga','jacareacanga','municipality'),
('Jacundá','jacunda','municipality'),
('Juruti','juruti','municipality'),
('Limoeiro do Ajuru','limoeiro-do-ajuru','municipality'),
('Mãe do Rio','mae-do-rio','municipality'),
('Magalhães Barata','magalhaes-barata','municipality'),
('Marabá','maraba','municipality'),
('Maracanã','maracana','municipality'),
('Marapanim','marapanim','municipality'),
('Marituba','marituba','municipality'),
('Medicilândia','medicilandia','municipality'),
('Melgaço','melgaco','municipality'),
('Mocajuba','mocajuba','municipality'),
('Moju','moju','municipality'),
('Mojuí dos Campos','mojui-dos-campos','municipality'),
('Monte Alegre','monte-alegre','municipality'),
('Muaná','muana','municipality'),
('Nova Esperança do Piriá','nova-esperanca-do-piria','municipality'),
('Nova Ipixuna','nova-ipixuna','municipality'),
('Nova Timboteua','nova-timboteua','municipality'),
('Novo Progresso','novo-progresso','municipality'),
('Novo Repartimento','novo-repartimento','municipality'),
('Óbidos','obidos','municipality'),
('Oeiras do Pará','oeiras-do-para','municipality'),
('Oriximiná','oriximina','municipality'),
('Ourém','ourem','municipality'),
('Ourilândia do Norte','ourilandia-do-norte','municipality'),
('Pacajá','pacaja','municipality'),
('Palestina do Pará','palestina-do-para','municipality'),
('Paragominas','paragominas','municipality'),
('Parauapebas','parauapebas','municipality'),
('Pau D''Arco','pau-d-arco','municipality'),
('Peixe-Boi','peixe-boi','municipality'),
('Piçarra','picarra','municipality'),
('Placas','placas','municipality'),
('Ponta de Pedras','ponta-de-pedras','municipality'),
('Portel','portel','municipality'),
('Porto de Moz','porto-de-moz','municipality'),
('Prainha','prainha','municipality'),
('Primavera','primavera','municipality'),
('Quatipuru','quatipuru','municipality'),
('Redenção','redencao','municipality'),
('Rio Maria','rio-maria','municipality'),
('Rondon do Pará','rondon-do-para','municipality'),
('Rurópolis','ruropolis','municipality'),
('Salinópolis','salinopolis','municipality'),
('Salvaterra','salvaterra','municipality'),
('Santa Bárbara do Pará','santa-barbara-do-para','municipality'),
('Santa Cruz do Arari','santa-cruz-do-arari','municipality'),
('Santa Izabel do Pará','santa-izabel-do-para','municipality'),
('Santa Luzia do Pará','santa-luzia-do-para','municipality'),
('Santa Maria das Barreiras','santa-maria-das-barreiras','municipality'),
('Santa Maria do Pará','santa-maria-do-para','municipality'),
('Santana do Araguaia','santana-do-araguaia','municipality'),
('Santarém','santarem','municipality'),
('Santarém Novo','santarem-novo','municipality'),
('Santo Antônio do Tauá','santo-antonio-do-taua','municipality'),
('São Caetano de Odivelas','sao-caetano-de-odivelas','municipality'),
('São Domingos do Araguaia','sao-domingos-do-araguaia','municipality'),
('São Domingos do Capim','sao-domingos-do-capim','municipality'),
('São Félix do Xingu','sao-felix-do-xingu','municipality'),
('São Francisco do Pará','sao-francisco-do-para','municipality'),
('São Geraldo do Araguaia','sao-geraldo-do-araguaia','municipality'),
('São João da Ponta','sao-joao-da-ponta','municipality'),
('São João de Pirabas','sao-joao-de-pirabas','municipality'),
('São João do Araguaia','sao-joao-do-araguaia','municipality'),
('São Miguel do Guamá','sao-miguel-do-guama','municipality'),
('São Sebastião da Boa Vista','sao-sebastiao-da-boa-vista','municipality'),
('Sapucaia','sapucaia','municipality'),
('Senador José Porfírio','senador-jose-porfirio','municipality'),
('Soure','soure','municipality'),
('Tailândia','tailandia','municipality'),
('Terra Alta','terra-alta','municipality'),
('Terra Santa','terra-santa','municipality'),
('Tomé-Açu','tome-acu','municipality'),
('Tracuateua','tracuateua','municipality'),
('Trairão','trairao','municipality'),
('Tucumã','tucuma','municipality'),
('Tucuruí','tucurui','municipality'),
('Ulianópolis','ulianopolis','municipality'),
('Uruará','uruara','municipality'),
('Vigia','vigia','municipality'),
('Viseu','viseu','municipality'),
('Vitória do Xingu','vitoria-do-xingu','municipality'),
('Xinguara','xinguara','municipality'),
('Cidade Velha','cidade-velha','neighborhood'),
('Campina','campina','neighborhood'),
('Reduto','reduto','neighborhood'),
('Umarizal','umarizal','neighborhood'),
('Telegrafo','telegrafo','neighborhood'),
('Sacramenta','sacramenta','neighborhood'),
('Pedreira','pedreira','neighborhood'),
('Marco','marco','neighborhood'),
('Souza','souza','neighborhood'),
('Marambaia','marambaia','neighborhood'),
('Canudos','canudos','neighborhood'),
('Fátima','fatima','neighborhood'),
('São Brás','sao-bras','neighborhood'),
('Nazaré','nazare','neighborhood'),
('Batista Campos','batista-campos','neighborhood'),
('Jurunas','jurunas','neighborhood'),
('Condor','condor','neighborhood'),
('Guamá','guama','neighborhood'),
('Terra Firme','terra-firme','neighborhood'),
('Cremação','cremacao','neighborhood'),
('Val-de-Cães','val-de-caes','neighborhood'),
('Miramar','miramar','neighborhood'),
('Pratinha','pratinha','neighborhood'),
('Tapanã','tapana','neighborhood'),
('Bengui','bengui','neighborhood'),
('Maracangalha','maracangalha','neighborhood'),
('Barreiro','barreiro','neighborhood'),
('Universitário','universitario','neighborhood'),
('Curió-Utinga','curio-utinga','neighborhood'),
('Aurá','aura','neighborhood'),
('Castanheira','castanheira','neighborhood'),
('Águas Lindas','aguas-lindas','neighborhood'),
('Guanabara','guanabara','neighborhood'),
('São Clemente','sao-clemente','neighborhood'),
('Parque Guajará','parque-guajara','neighborhood'),
('Tenoné','tenone','neighborhood'),
('Águas Negras','aguas-negras','neighborhood'),
('Maracacuera','maracacuera','neighborhood'),
('Parque Verde','parque-verde','neighborhood'),
('Cruzeiro','cruzeiro','neighborhood'),
('Ponta Grossa','ponta-grossa','neighborhood'),
('Mangueirão','mangueirao','neighborhood'),
('Cabanagem','cabanagem','neighborhood'),
('Campina de Icoaraci','campina-de-icoaraci','neighborhood'),
('Paracuri','paracuri','neighborhood'),
('Agulha','agulha','neighborhood'),
('Una','una','neighborhood'),
('Coqueiro','coqueiro','neighborhood'),
('Brasília','brasilia','neighborhood'),
('São João do Outeiro','sao-joao-do-outeiro','neighborhood'),
('Itaiteua','itaiteua','neighborhood'),
('Água Negra','agua-negra','neighborhood'),
('Maracajá','maracaja','neighborhood'),
('Vila','vila','neighborhood'),
('Praia Grande','praia-grande','neighborhood'),
('Farol','farol','neighborhood'),
('Mangueiras','mangueiras','neighborhood'),
('São Francisco','sao-francisco','neighborhood'),
('Caranamduba','caranamduba','neighborhood'),
('Marahu','marahu','neighborhood'),
('Paraíso','paraiso','neighborhood'),
('Baia do Sol','baia-do-sol','neighborhood'),
('Sucurijuquara','sucurijuquara','neighborhood'),
('Caruara','caruara','neighborhood'),
('Bonfim','bonfim','neighborhood'),
('Ariramba','ariramba','neighborhood'),
('Porto Arthur','porto-arthur','neighborhood'),
('Murubira','murubira','neighborhood'),
('Chapéu Virado','chapeu-virado','neighborhood'),
('Aeroporto','aeroporto','neighborhood');

-- @migration 006_midia
CREATE TABLE IF NOT EXISTS mb_media_permissions (
 source_id BIGINT UNSIGNED NOT NULL PRIMARY KEY,
 allow_analysis TINYINT(1) NOT NULL DEFAULT 0,
 license_note VARCHAR(1000) NOT NULL,
 CONSTRAINT fk_media_permission_source FOREIGN KEY(source_id) REFERENCES mb_ingestion_sources(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS mb_media_analysis (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
 candidate_id BIGINT UNSIGNED NOT NULL,
 actor_id BIGINT UNSIGNED NOT NULL,
 engine VARCHAR(80) NOT NULL,
 extracted_text TEXT NOT NULL,
 credit VARCHAR(500) NOT NULL,
 license_note VARCHAR(1000) NOT NULL,
 created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 KEY idx_media_candidate(candidate_id,created_at),
 CONSTRAINT fk_media_candidate FOREIGN KEY(candidate_id) REFERENCES mb_ingestion_candidates(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- @migration 007_relacionamentos
-- Validacao aditiva: dados orfaos fazem a operacao parar; nenhum registro e apagado.

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_content_source'),'SELECT 1','ALTER TABLE mb_content_items ADD CONSTRAINT fk_mb_content_source FOREIGN KEY (source_id) REFERENCES mb_ingestion_sources(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_content_reviewer'),'SELECT 1','ALTER TABLE mb_content_items ADD CONSTRAINT fk_mb_content_reviewer FOREIGN KEY (reviewed_by_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_content_author'),'SELECT 1','ALTER TABLE mb_content_items ADD CONSTRAINT fk_mb_content_author FOREIGN KEY (created_by_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_revision_actor'),'SELECT 1','ALTER TABLE mb_content_revisions ADD CONSTRAINT fk_mb_revision_actor FOREIGN KEY (changed_by_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_candidate_content'),'SELECT 1','ALTER TABLE mb_ingestion_candidates ADD CONSTRAINT fk_mb_candidate_content FOREIGN KEY (content_id) REFERENCES mb_content_items(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_audit_actor'),'SELECT 1','ALTER TABLE mb_audit_logs ADD CONSTRAINT fk_mb_audit_actor FOREIGN KEY (actor_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_ai_actor'),'SELECT 1','ALTER TABLE mb_ai_runs ADD CONSTRAINT fk_mb_ai_actor FOREIGN KEY (triggered_by_user_id) REFERENCES mb_users(id) ON DELETE SET NULL');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;

SET @minhabelem_ddl = IF(EXISTS(SELECT 1 FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME='fk_mb_media_actor'),'SELECT 1','ALTER TABLE mb_media_analysis ADD CONSTRAINT fk_mb_media_actor FOREIGN KEY (actor_id) REFERENCES mb_users(id) ON DELETE RESTRICT');
PREPARE minhabelem_stmt FROM @minhabelem_ddl;
EXECUTE minhabelem_stmt;
DEALLOCATE PREPARE minhabelem_stmt;
