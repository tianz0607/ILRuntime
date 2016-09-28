﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Mono.Cecil;
using ILRuntime.Runtime.Intepreter.OpCodes;
using ILRuntime.CLR.TypeSystem;
namespace ILRuntime.CLR.Method
{
    class ILMethod : IMethod
    {
        OpCode[] body;
        MethodDefinition def;
        List<IType> parameters;
        ILRuntime.Runtime.Enviorment.AppDomain appdomain;
        ILType declaringType;
        ExceptionHandler[] exceptionHandler;
        KeyValuePair<string, IType>[] genericParameters;
        Dictionary<int, int[]> jumptables;
        bool isDelegateInvoke;

        public MethodDefinition Definition { get { return def; } }

        public Dictionary<int, int[]> JumpTables { get { return jumptables; } }

        public ExceptionHandler[] ExceptionHandler
        {
            get
            {
                if (body == null)
                    InitCodeBody(); 
                return exceptionHandler;
            }
        }

        public string Name
        {
            get
            {
                return def.Name;
            }
        }

        public IType DeclearingType
        {
            get
            {
                return declaringType;
            }
        }

        public bool HasThis
        {
            get
            {
                return def.HasThis;
            }
        }
        public int GenericParameterCount
        {
            get
            {
                return def.GenericParameters.Count;
            }
        }
        public bool IsGenericInstance
        {
            get
            {
                return genericParameters != null;
            }
        }
        public ILMethod(MethodDefinition def, ILType type, ILRuntime.Runtime.Enviorment.AppDomain domain)
        {
            this.def = def;
            declaringType = type;
            if (def.ReturnType.IsGenericParameter)
            {
                ReturnType = FindGenericArgument(def.ReturnType.Name);
            }
            else
                ReturnType = domain.GetType(def.ReturnType, type);
            if (type.IsDelegate && def.Name == "Invoke")
                isDelegateInvoke = true;
            this.appdomain = domain;
        }

        IType FindGenericArgument(string name)
        {
            IType res = declaringType.FindGenericArgument(name);
            if (res == null && genericParameters != null)
            {
                foreach (var i in genericParameters)
                {
                    if (i.Key == name)
                        return i.Value;
                }
            }
            else
                return res;
            return null;
        }

        public OpCode[] Body
        {
            get
            {
                if (body == null)
                    InitCodeBody();
                return body;
            }
        }

        public int LocalVariableCount
        {
            get
            {
                return def.HasBody ? def.Body.Variables.Count : 0;
            }
        }

        public bool IsConstructor
        {
            get
            {
                return def.IsConstructor;
            }
        }

        public bool IsDelegateInvoke
        {
            get
            {
                return isDelegateInvoke;
            }
        }

        public int ParameterCount
        {
            get
            {
                return def.HasParameters ? def.Parameters.Count : 0;
            }
        }


        public List<IType> Parameters
        {
            get
            {
                if (def.HasParameters && parameters == null)
                {
                    InitParameters();
                }
                return parameters;
            }
        }

        public IType ReturnType
        {
            get;
            private set;
        }
        void InitCodeBody()
        {
            if (def.HasBody)
            {
                body = new OpCode[def.Body.Instructions.Count];
                Dictionary<Mono.Cecil.Cil.Instruction, int> addr = new Dictionary<Mono.Cecil.Cil.Instruction, int>();
                for (int i = 0; i < body.Length; i++)
                {
                    var c = def.Body.Instructions[i];
                    OpCode code = new OpCode();
                    code.Code = (OpCodeEnum)c.OpCode.Code;
                    addr[c] = i;
                    body[i] = code;
                }
                for (int i = 0; i < body.Length; i++)
                {
                    var c = def.Body.Instructions[i];
                    InitToken(ref body[i], c.Operand, addr);
                }

                for (int i = 0; i < def.Body.ExceptionHandlers.Count; i++)
                {
                    var eh = def.Body.ExceptionHandlers[i];
                    if (exceptionHandler == null)
                        exceptionHandler = new Method.ExceptionHandler[def.Body.ExceptionHandlers.Count];
                    ExceptionHandler e = new ExceptionHandler();
                    e.HandlerStart = addr[eh.HandlerStart];
                    e.HandlerEnd = addr[eh.HandlerEnd] - 1;
                    e.TryStart = addr[eh.TryStart];
                    e.TryEnd = addr[eh.TryEnd] - 1;
                    switch (eh.HandlerType)
                    {
                        case Mono.Cecil.Cil.ExceptionHandlerType.Catch:
                            e.CatchType = appdomain.GetType(eh.CatchType, declaringType);
                            e.HandlerType = ExceptionHandlerType.Catch;
                            break;
                        case Mono.Cecil.Cil.ExceptionHandlerType.Finally:
                            e.HandlerType = ExceptionHandlerType.Finally;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    exceptionHandler[i] = e;
                    //Mono.Cecil.Cil.ExceptionHandlerType.
                }
            }
            else
                body = new OpCode[0];
        }

        void InitToken(ref OpCode code, object token, Dictionary<Mono.Cecil.Cil.Instruction, int> addr)
        {
            switch (code.Code)
            {
                case OpCodeEnum.Leave:
                case OpCodeEnum.Leave_S:
                case OpCodeEnum.Br:
                case OpCodeEnum.Br_S:
                case OpCodeEnum.Brtrue:
                case OpCodeEnum.Brtrue_S:
                case OpCodeEnum.Brfalse:
                case OpCodeEnum.Brfalse_S:
                //比较流程控制
                case OpCodeEnum.Beq:
                case OpCodeEnum.Beq_S:
                case OpCodeEnum.Bne_Un:
                case OpCodeEnum.Bne_Un_S:
                case OpCodeEnum.Bge:
                case OpCodeEnum.Bge_S:
                case OpCodeEnum.Bge_Un:
                case OpCodeEnum.Bge_Un_S:
                case OpCodeEnum.Bgt:
                case OpCodeEnum.Bgt_S:
                case OpCodeEnum.Bgt_Un:
                case OpCodeEnum.Bgt_Un_S:
                case OpCodeEnum.Ble:
                case OpCodeEnum.Ble_S:
                case OpCodeEnum.Ble_Un:
                case OpCodeEnum.Ble_Un_S:
                case OpCodeEnum.Blt:
                case OpCodeEnum.Blt_S:
                case OpCodeEnum.Blt_Un:
                case OpCodeEnum.Blt_Un_S:
                    code.TokenInteger = addr[(Mono.Cecil.Cil.Instruction)token]; 
                    break;
                case OpCodeEnum.Ldc_I4:
                    code.TokenInteger = (int)token;
                    break;
                case OpCodeEnum.Ldc_I4_S:
                    code.TokenInteger = (sbyte)token;
                    break;
                case OpCodeEnum.Ldc_I8:
                    code.TokenLong = (long)token;
                    break;
                case OpCodeEnum.Ldc_R4:
                    code.TokenFloat = (float)token;
                    break;
                case OpCodeEnum.Ldc_R8:
                    code.TokenDouble = (double)token;
                    break;                    
                case OpCodeEnum.Stloc:
                case OpCodeEnum.Stloc_S:
                case OpCodeEnum.Ldloc:
                case OpCodeEnum.Ldloc_S:
                case OpCodeEnum.Ldloca:
                case OpCodeEnum.Ldloca_S:
                    {
                        Mono.Cecil.Cil.VariableDefinition vd = (Mono.Cecil.Cil.VariableDefinition)token;
                        code.TokenInteger = vd.Index;
                    }
                    break;
                case OpCodeEnum.Ldarg_S:
                case OpCodeEnum.Ldarg:
                case OpCodeEnum.Ldarga:
                case OpCodeEnum.Ldarga_S:
                case OpCodeEnum.Starg:
                case OpCodeEnum.Starg_S:
                    {
                        Mono.Cecil.ParameterDefinition vd = (Mono.Cecil.ParameterDefinition)token;
                        code.TokenInteger = vd.Index;
                        if (HasThis)
                            code.TokenInteger++;
                    }
                    break;
                case OpCodeEnum.Call:
                case OpCodeEnum.Newobj:
                case OpCodeEnum.Ldftn:
                case OpCodeEnum.Callvirt:
                    {
                        var m = appdomain.GetMethod(token, declaringType);
                        if (m != null)
                        {
                            if (m.IsGenericInstance)
                                code.TokenInteger = m.GetHashCode();
                            else
                                code.TokenInteger = token.GetHashCode();
                        }
                    }
                    break;
                case OpCodeEnum.Constrained:
                case OpCodeEnum.Box:
                case OpCodeEnum.Unbox_Any:
                case OpCodeEnum.Unbox:
                case OpCodeEnum.Initobj:
                case OpCodeEnum.Isinst:
                case OpCodeEnum.Newarr:
                case OpCodeEnum.Stobj:
                case OpCodeEnum.Ldobj:
                    {
                        var t = appdomain.GetType(token, declaringType);
                        if (t == null && token is TypeReference && ((TypeReference)token).IsGenericParameter)
                        {
                            t = FindGenericArgument(((TypeReference)token).Name);
                        }
                        if (t != null)
                        {
                            if (t is ILType)
                            {
                                code.TokenInteger = ((ILType)t).TypeReference.GetHashCode();
                            }
                            else
                                code.TokenInteger = token.GetHashCode();
                        }
                    }
                    break;
                case OpCodeEnum.Stfld:
                case OpCodeEnum.Ldfld:
                case OpCodeEnum.Ldflda:
                    {
                        code.TokenInteger = appdomain.GetFieldIndex(token, declaringType);   
                    }
                    break;

                case OpCodeEnum.Stsfld:
                case OpCodeEnum.Ldsfld:
                case OpCodeEnum.Ldsflda:
                    {
                        code.TokenLong = appdomain.GetStaticFieldIndex(token, declaringType);   
                    }
                    break;
                case OpCodeEnum.Ldstr:
                    {
                        int hashCode = token.GetHashCode();
                        appdomain.CacheString(token);
                        code.TokenInteger = hashCode;
                    }
                    break;
                case OpCodeEnum.Ldtoken:
                    {
                        if (token is FieldReference)
                        {
                            code.TokenInteger = 0;
                            code.TokenLong = appdomain.GetStaticFieldIndex(token, declaringType);
                        }
                        else
                            throw new NotImplementedException();
                    }
                    break;
                case OpCodeEnum.Switch:
                    {
                        PrepareJumpTable(token, addr);
                        code.TokenInteger = token.GetHashCode();
                    }
                    break;
            }
        }

        void PrepareJumpTable(object token,Dictionary<Mono.Cecil.Cil.Instruction, int> addr)
        {
            int hashCode = token.GetHashCode();

            if (jumptables == null)
                jumptables = new Dictionary<int, int[]>();
            if (jumptables.ContainsKey(hashCode))
                return;
            Mono.Cecil.Cil.Instruction[] e = token as Mono.Cecil.Cil.Instruction[];
            int[] addrs = new int[e.Length];
            for (int i = 0; i < e.Length; i++)
            {
                addrs[i] = addr[e[i]];
            }

            jumptables[hashCode] = addrs;
        }

        void InitParameters()
        {
            parameters = new List<IType>();
            foreach (var i in def.Parameters)
            {
                IType type = null;
                
                if (i.ParameterType.IsGenericParameter)
                {
                    type = FindGenericArgument(i.ParameterType.Name);
                    if (type == null && def.HasGenericParameters)
                    {
                        bool found = false;
                        foreach (var j in def.GenericParameters)
                        {
                            if (j.Name == i.ParameterType.Name)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (found)
                        {
                            type = new ILGenericParameterType(i.ParameterType.Name);
                        }
                        else
                            throw new NotSupportedException("Cannot find Generic Parameter " + i.ParameterType.Name + " in " + def.FullName);
                    }
                }
                else
                    type = appdomain.GetType(i.ParameterType, declaringType);
                parameters.Add(type);
            }
        }

        public IMethod MakeGenericMethod(IType[] genericArguments)
        {
            KeyValuePair<string, IType>[] genericParameters = new KeyValuePair<string, IType>[genericArguments.Length];
            for (int i = 0; i < genericArguments.Length; i++)
            {
                string name = def.GenericParameters[i].Name;
                IType val = genericArguments[i];
                genericParameters[i] = new KeyValuePair<string, IType>(name, val);
            }

            ILMethod m = new ILMethod(def, declaringType, appdomain);
            m.genericParameters = genericParameters;

            return m;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(declaringType.FullName);
            sb.Append('.');
            sb.Append(Name);
            sb.Append('(');
            bool isFirst = true;
            if (parameters == null)
                InitParameters();
            for (int i = 0; i < parameters.Count; i++)
            {
                if (isFirst)
                    isFirst = false;
                else
                    sb.Append(", ");
                sb.Append(parameters[i].Name);
                sb.Append(' ');
                sb.Append(def.Parameters[i].Name);
            }
            sb.Append(')');
            return sb.ToString();
        }
    }
}
