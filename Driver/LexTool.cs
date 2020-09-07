﻿using System;
using System.Collections.Generic;
using System.IO;
using MonC;

namespace Driver
{
    [CommandLineCategory("Lex")]
    public abstract class LexTool : ITool, IParseInput
    {
        [CommandLine("-showlex", "Show lexer tokens while processing")]
        private bool _showLex = false;
        
        protected void Lex(string input, Lexer lexer, List<Token> tokens)
        {
            List<Token> newTokens = new List<Token>();
            lexer.Lex(input, newTokens);

            if (_showLex) {
                for (int i = 0, ilen = newTokens.Count; i < ilen; ++i) {
                    Console.WriteLine(newTokens[i]);
                }
            }

            tokens.AddRange(newTokens);
        }

        public virtual List<Token> GetTokens() => throw new NotImplementedException();

        public virtual string GetFilename() => throw new NotImplementedException();

        public virtual void WriteInputChain(TextWriter writer) => throw new NotImplementedException();

        public static LexTool Construct(Job job, ILexInput input)
        {
            if (input is FileInfo info) {
                return info.IsInteractive ? (LexTool) new InteractiveLexTool() : new FileLexTool(info);
            }

            throw new InvalidOperationException($"{input.GetType()} not supported for LexTool");
        }
    }

    public sealed class FileLexTool : LexTool
    {
        private FileInfo _fileInfo;
        
        internal FileLexTool(FileInfo fileInfo) => _fileInfo = fileInfo;

        public override List<Token> GetTokens()
        {
            List<Token> tokens = new List<Token>();
            string input; 
            Lexer lexer = new Lexer();

            TextReader reader = _fileInfo.GetTextReader();
            while ((input = reader.ReadLine()) != null) {
                Lex(input, lexer, tokens);
                Lex("\n", lexer, tokens);
            }

            return tokens;
        }

        public override string GetFilename() => _fileInfo.FullPath;

        public override void WriteInputChain(TextWriter writer)
        {
            _fileInfo.WriteInputChain(writer);
            writer.WriteLine("  -FileLexTool");
        }
    }

    public sealed class InteractiveLexTool : LexTool
    {
        private static void WritePrompt()
        {
            Console.Write("> ");
            Console.Out.Flush();
        }

        public override List<Token> GetTokens()
        {
            List<Token> tokens = new List<Token>();
            string input;
            Lexer lexer = new Lexer();

            WritePrompt();
            while ((input = Console.ReadLine()) != null) {
                Lex(input, lexer, tokens);
                Lex("\n", lexer, tokens);
                WritePrompt();
            }

            return tokens;
        }

        public override string GetFilename() => null;

        public override void WriteInputChain(TextWriter writer)
        {
            writer.WriteLine("  -InteractiveLexTool");
        }
    }
}