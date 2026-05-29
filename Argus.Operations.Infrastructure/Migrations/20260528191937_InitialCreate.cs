using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Argus.Operations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BRIGADA",
                columns: table => new
                {
                    ID_BRIGADA = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    BASE_OPERACIONAL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ATIVA = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BRIGADA", x => x.ID_BRIGADA);
                });

            migrationBuilder.CreateTable(
                name: "USUARIO",
                columns: table => new
                {
                    ID_USUARIO = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    SENHA_HASH = table.Column<string>(type: "NVARCHAR2(255)", maxLength: 255, nullable: false),
                    PERFIL = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DATA_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ULTIMO_LOGIN = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USUARIO", x => x.ID_USUARIO);
                });

            migrationBuilder.CreateTable(
                name: "BRIGADISTA",
                columns: table => new
                {
                    ID_BRIGADISTA = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    MATRICULA = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    TELEFONE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    FUNCAO = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DATA_ADMISSAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ID_BRIGADA = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BRIGADISTA", x => x.ID_BRIGADISTA);
                    table.ForeignKey(
                        name: "FK_BRIGADISTA_BRIGADA_ID_BRIGADA",
                        column: x => x.ID_BRIGADA,
                        principalTable: "BRIGADA",
                        principalColumn: "ID_BRIGADA",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RECURSO",
                columns: table => new
                {
                    ID_RECURSO = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    NOME = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DISPONIVEL = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ID_BRIGADA = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RECURSO", x => x.ID_RECURSO);
                    table.ForeignKey(
                        name: "FK_RECURSO_BRIGADA_ID_BRIGADA",
                        column: x => x.ID_BRIGADA,
                        principalTable: "BRIGADA",
                        principalColumn: "ID_BRIGADA",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OCORRENCIA",
                columns: table => new
                {
                    ID_OCORRENCIA = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DESCRICAO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    LATITUDE = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    LONGITUDE = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA_ABERTURA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DATA_FINALIZACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ID_BRIGADISTA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_BRIGADA = table.Column<long>(type: "NUMBER(19)", nullable: false),
                    ID_ALERTA = table.Column<long>(type: "NUMBER(19)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OCORRENCIA", x => x.ID_OCORRENCIA);
                    table.ForeignKey(
                        name: "FK_OCORRENCIA_BRIGADA_ID_BRIGADA",
                        column: x => x.ID_BRIGADA,
                        principalTable: "BRIGADA",
                        principalColumn: "ID_BRIGADA",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OCORRENCIA_BRIGADISTA_ID_BRIGADISTA",
                        column: x => x.ID_BRIGADISTA,
                        principalTable: "BRIGADISTA",
                        principalColumn: "ID_BRIGADISTA",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "REGISTRO_CAMPO",
                columns: table => new
                {
                    ID_REGISTRO_CAMPO = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    OBSERVACAO = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    URL_FOTO = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: false),
                    LATITUDE = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    LONGITUDE = table.Column<double>(type: "BINARY_DOUBLE", nullable: false),
                    DATA_REGISTRO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ID_OCORRENCIA = table.Column<long>(type: "NUMBER(19)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_REGISTRO_CAMPO", x => x.ID_REGISTRO_CAMPO);
                    table.ForeignKey(
                        name: "FK_REGISTRO_CAMPO_OCORRENCIA_ID_OCORRENCIA",
                        column: x => x.ID_OCORRENCIA,
                        principalTable: "OCORRENCIA",
                        principalColumn: "ID_OCORRENCIA",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BRIGADISTA_ID_BRIGADA",
                table: "BRIGADISTA",
                column: "ID_BRIGADA");

            migrationBuilder.CreateIndex(
                name: "IX_OCORRENCIA_ID_BRIGADA",
                table: "OCORRENCIA",
                column: "ID_BRIGADA");

            migrationBuilder.CreateIndex(
                name: "IX_OCORRENCIA_ID_BRIGADISTA",
                table: "OCORRENCIA",
                column: "ID_BRIGADISTA");

            migrationBuilder.CreateIndex(
                name: "IX_RECURSO_ID_BRIGADA",
                table: "RECURSO",
                column: "ID_BRIGADA");

            migrationBuilder.CreateIndex(
                name: "IX_REGISTRO_CAMPO_ID_OCORRENCIA",
                table: "REGISTRO_CAMPO",
                column: "ID_OCORRENCIA");

            migrationBuilder.CreateIndex(
                name: "IX_USUARIO_EMAIL",
                table: "USUARIO",
                column: "EMAIL",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RECURSO");

            migrationBuilder.DropTable(
                name: "REGISTRO_CAMPO");

            migrationBuilder.DropTable(
                name: "USUARIO");

            migrationBuilder.DropTable(
                name: "OCORRENCIA");

            migrationBuilder.DropTable(
                name: "BRIGADISTA");

            migrationBuilder.DropTable(
                name: "BRIGADA");
        }
    }
}
