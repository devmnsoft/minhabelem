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
