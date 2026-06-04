-- =============================================================================
-- Argus — Script de seed de dados de teste
-- =============================================================================
-- Insere um conjunto coeso pra exercitar o mobile end-to-end:
--   3 brigadas em regiões distintas
--   6 brigadistas (2 por brigada)
--   4 recursos (variando tipo e disponibilidade)
--   4 ocorrências em status diferentes (Aberta, EmAtendimento, Controlada, Finalizada)
--   6 registros de campo (1-2 por ocorrência)
--
-- Não toca em USUARIO — quem cuida disso é o AdminSeeder do .NET no startup.
--
-- Como rodar:
--   No SQL Developer / DBeaver / sqlplus, executa este arquivo INTEIRO de uma vez.
--   Os IDs são gerados pelas sequences (RETURNING INTO), então não conflita
--   com dados existentes.
--
-- Como limpar (se quiser repetir):
--   DELETE FROM REGISTRO_CAMPO;
--   DELETE FROM OCORRENCIA;
--   DELETE FROM RECURSO;
--   DELETE FROM BRIGADISTA;
--   DELETE FROM BRIGADA;
--   COMMIT;
-- =============================================================================

SET SERVEROUTPUT ON;

DECLARE
    -- IDs das brigadas
    v_brigada_pantanal  NUMBER;
    v_brigada_cerrado   NUMBER;
    v_brigada_amazonia  NUMBER;

    -- IDs dos brigadistas (2 por brigada)
    v_brig_maria        NUMBER;
    v_brig_carlos       NUMBER;
    v_brig_ana          NUMBER;
    v_brig_joao         NUMBER;
    v_brig_lucia        NUMBER;
    v_brig_pedro        NUMBER;

    -- IDs das ocorrências
    v_ocorr_pantanal_1  NUMBER;
    v_ocorr_pantanal_2  NUMBER;
    v_ocorr_cerrado     NUMBER;
    v_ocorr_amazonia    NUMBER;
BEGIN
    -- ===================== BRIGADAS =====================
    INSERT INTO BRIGADA (NOME, BASE_OPERACIONAL, TELEFONE, ATIVA)
    VALUES ('Brigada Argus Pantanal Norte', 'Corumbá, MS', '6732311234', 1)
    RETURNING ID_BRIGADA INTO v_brigada_pantanal;

    INSERT INTO BRIGADA (NOME, BASE_OPERACIONAL, TELEFONE, ATIVA)
    VALUES ('Brigada Argus Cerrado Central', 'Brasília, DF', '6133344455', 1)
    RETURNING ID_BRIGADA INTO v_brigada_cerrado;

    INSERT INTO BRIGADA (NOME, BASE_OPERACIONAL, TELEFONE, ATIVA)
    VALUES ('Brigada Argus Amazônia Sul', 'Porto Velho, RO', '6932655566', 1)
    RETURNING ID_BRIGADA INTO v_brigada_amazonia;

    -- ===================== BRIGADISTAS =====================
    -- Pantanal
    INSERT INTO BRIGADISTA (NOME, MATRICULA, EMAIL, TELEFONE, FUNCAO, ATIVO, DATA_ADMISSAO, ID_BRIGADA)
    VALUES ('Maria Silva', 'BRG-PT-001', 'maria.silva@argus.com', '67999887766',
            'Líder de Esquadrão', 1, SYSDATE - 365, v_brigada_pantanal)
    RETURNING ID_BRIGADISTA INTO v_brig_maria;

    INSERT INTO BRIGADISTA (NOME, MATRICULA, EMAIL, TELEFONE, FUNCAO, ATIVO, DATA_ADMISSAO, ID_BRIGADA)
    VALUES ('Carlos Mendes', 'BRG-PT-002', 'carlos.mendes@argus.com', '67998776655',
            'Brigadista Florestal', 1, SYSDATE - 200, v_brigada_pantanal)
    RETURNING ID_BRIGADISTA INTO v_brig_carlos;

    -- Cerrado
    INSERT INTO BRIGADISTA (NOME, MATRICULA, EMAIL, TELEFONE, FUNCAO, ATIVO, DATA_ADMISSAO, ID_BRIGADA)
    VALUES ('Ana Oliveira', 'BRG-CE-001', 'ana.oliveira@argus.com', '61999112233',
            'Líder de Esquadrão', 1, SYSDATE - 540, v_brigada_cerrado)
    RETURNING ID_BRIGADISTA INTO v_brig_ana;

    INSERT INTO BRIGADISTA (NOME, MATRICULA, EMAIL, TELEFONE, FUNCAO, ATIVO, DATA_ADMISSAO, ID_BRIGADA)
    VALUES ('João Almeida', 'BRG-CE-002', 'joao.almeida@argus.com', '61998223344',
            'Operador de Bomba', 1, SYSDATE - 150, v_brigada_cerrado)
    RETURNING ID_BRIGADISTA INTO v_brig_joao;

    -- Amazônia
    INSERT INTO BRIGADISTA (NOME, MATRICULA, EMAIL, TELEFONE, FUNCAO, ATIVO, DATA_ADMISSAO, ID_BRIGADA)
    VALUES ('Lúcia Ferreira', 'BRG-AM-001', 'lucia.ferreira@argus.com', '69999556677',
            'Líder de Esquadrão', 1, SYSDATE - 420, v_brigada_amazonia)
    RETURNING ID_BRIGADISTA INTO v_brig_lucia;

    INSERT INTO BRIGADISTA (NOME, MATRICULA, EMAIL, TELEFONE, FUNCAO, ATIVO, DATA_ADMISSAO, ID_BRIGADA)
    VALUES ('Pedro Santos', 'BRG-AM-002', 'pedro.santos@argus.com', '69998667788',
            'Brigadista Florestal', 1, SYSDATE - 90, v_brigada_amazonia)
    RETURNING ID_BRIGADISTA INTO v_brig_pedro;

    -- ===================== RECURSOS =====================
    -- TipoRecurso: 1=Veiculo, 2=Ferramenta, 3=EPI, 4=Comunicacao
    INSERT INTO RECURSO (NOME, TIPO, DISPONIVEL, ID_BRIGADA)
    VALUES ('Caminhão-pipa F-4000', 1, 1, v_brigada_pantanal);

    INSERT INTO RECURSO (NOME, TIPO, DISPONIVEL, ID_BRIGADA)
    VALUES ('Bomba costal McCulloch', 2, 1, v_brigada_pantanal);

    INSERT INTO RECURSO (NOME, TIPO, DISPONIVEL, ID_BRIGADA)
    VALUES ('Kit EPI completo (10 un.)', 3, 1, v_brigada_cerrado);

    INSERT INTO RECURSO (NOME, TIPO, DISPONIVEL, ID_BRIGADA)
    VALUES ('Rádio HT VHF dual-band', 4, 0, v_brigada_amazonia);

    -- ===================== OCORRÊNCIAS =====================
    -- StatusOcorrencia: 1=Aberta, 2=EmAtendimento, 3=Controlada, 4=Finalizada

    -- 1) Aberta, sem registros ainda — recém detectada
    INSERT INTO OCORRENCIA (DESCRICAO, LATITUDE, LONGITUDE, STATUS, DATA_ABERTURA, DATA_FINALIZACAO, ID_BRIGADISTA, ID_BRIGADA, ID_ALERTA)
    VALUES ('Foco em vegetação seca próximo à BR-262, área de fácil acesso',
            -19.0084, -57.6531, 1, SYSDATE - INTERVAL '2' HOUR, NULL,
            v_brig_maria, v_brigada_pantanal, NULL)
    RETURNING ID_OCORRENCIA INTO v_ocorr_pantanal_1;

    -- 2) EmAtendimento — equipe no local
    INSERT INTO OCORRENCIA (DESCRICAO, LATITUDE, LONGITUDE, STATUS, DATA_ABERTURA, DATA_FINALIZACAO, ID_BRIGADISTA, ID_BRIGADA, ID_ALERTA)
    VALUES ('Incêndio de médio porte em pastagem, vento moderado SO',
            -19.0512, -57.7218, 2, SYSDATE - INTERVAL '6' HOUR, NULL,
            v_brig_carlos, v_brigada_pantanal, NULL)
    RETURNING ID_OCORRENCIA INTO v_ocorr_pantanal_2;

    -- 3) Controlada — em fase de rescaldo
    INSERT INTO OCORRENCIA (DESCRICAO, LATITUDE, LONGITUDE, STATUS, DATA_ABERTURA, DATA_FINALIZACAO, ID_BRIGADISTA, ID_BRIGADA, ID_ALERTA)
    VALUES ('Queimada controlada em área de cerrado denso',
            -15.7935, -47.8825, 3, SYSDATE - 1, NULL,
            v_brig_ana, v_brigada_cerrado, NULL)
    RETURNING ID_OCORRENCIA INTO v_ocorr_cerrado;

    -- 4) Finalizada — concluída há 3 dias
    INSERT INTO OCORRENCIA (DESCRICAO, LATITUDE, LONGITUDE, STATUS, DATA_ABERTURA, DATA_FINALIZACAO, ID_BRIGADISTA, ID_BRIGADA, ID_ALERTA)
    VALUES ('Foco em região de mata, atendido e extinto sem reignição',
            -8.7619, -63.9039, 4, SYSDATE - 4, SYSDATE - 3,
            v_brig_lucia, v_brigada_amazonia, NULL)
    RETURNING ID_OCORRENCIA INTO v_ocorr_amazonia;

    -- ===================== REGISTROS DE CAMPO =====================
    -- Sem registros pra a ocorrência 1 (acabou de abrir, ainda não tem ação em campo)

    -- Ocorrência 2 (EmAtendimento) — 2 registros
    INSERT INTO REGISTRO_CAMPO (OBSERVACAO, URL_FOTO, LATITUDE, LONGITUDE, DATA_REGISTRO, ID_OCORRENCIA)
    VALUES ('Equipe chegou ao local, iniciando contenção do perímetro leste',
            'https://placehold.co/600x400?text=Chegada+ao+local',
            -19.0512, -57.7218, SYSDATE - INTERVAL '5' HOUR, v_ocorr_pantanal_2);

    INSERT INTO REGISTRO_CAMPO (OBSERVACAO, URL_FOTO, LATITUDE, LONGITUDE, DATA_REGISTRO, ID_OCORRENCIA)
    VALUES ('Aceiro aberto no flanco sul, fogo recuando',
            'https://placehold.co/600x400?text=Aceiro+aberto',
            -19.0518, -57.7225, SYSDATE - INTERVAL '3' HOUR, v_ocorr_pantanal_2);

    -- Ocorrência 3 (Controlada) — 2 registros
    INSERT INTO REGISTRO_CAMPO (OBSERVACAO, URL_FOTO, LATITUDE, LONGITUDE, DATA_REGISTRO, ID_OCORRENCIA)
    VALUES ('Fogo de copa contido, iniciando rescaldo',
            'https://placehold.co/600x400?text=Rescaldo',
            -15.7935, -47.8825, SYSDATE - 1 + INTERVAL '4' HOUR, v_ocorr_cerrado);

    INSERT INTO REGISTRO_CAMPO (OBSERVACAO, URL_FOTO, LATITUDE, LONGITUDE, DATA_REGISTRO, ID_OCORRENCIA)
    VALUES ('Monitoramento de pontos quentes em curso',
            'https://placehold.co/600x400?text=Monitoramento',
            -15.7942, -47.8810, SYSDATE - INTERVAL '12' HOUR, v_ocorr_cerrado);

    -- Ocorrência 4 (Finalizada) — 2 registros
    INSERT INTO REGISTRO_CAMPO (OBSERVACAO, URL_FOTO, LATITUDE, LONGITUDE, DATA_REGISTRO, ID_OCORRENCIA)
    VALUES ('Atendimento iniciado, perímetro identificado',
            'https://placehold.co/600x400?text=Inicio',
            -8.7619, -63.9039, SYSDATE - 4 + INTERVAL '1' HOUR, v_ocorr_amazonia);

    INSERT INTO REGISTRO_CAMPO (OBSERVACAO, URL_FOTO, LATITUDE, LONGITUDE, DATA_REGISTRO, ID_OCORRENCIA)
    VALUES ('Foco extinto, equipe desmobilizada',
            'https://placehold.co/600x400?text=Extinto',
            -8.7619, -63.9039, SYSDATE - 3, v_ocorr_amazonia);

    COMMIT;

    DBMS_OUTPUT.PUT_LINE('Seed concluído com sucesso!');
    DBMS_OUTPUT.PUT_LINE('Brigadas criadas: Pantanal=' || v_brigada_pantanal ||
                         ', Cerrado=' || v_brigada_cerrado ||
                         ', Amazônia=' || v_brigada_amazonia);
    DBMS_OUTPUT.PUT_LINE('Brigadistas: 6 | Recursos: 4 | Ocorrências: 4 | Registros: 6');
END;
/
