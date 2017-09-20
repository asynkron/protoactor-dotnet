using System;
using System.Security.Cryptography;
using System.Text;

namespace Proto.Cluster
{
	/// <summary>
	/// A dotnet port of rendezvous.go
	/// </summary>
	public class Rendezvous
	{
		private static readonly HashAlgorithm HashAlgorithm = FNV1A32.Create();

		private class NodeScore
		{
			public byte[] node;
			public uint score;
		}

		private NodeScore[] nodes;

		public Rendezvous(string[] nodes)
		{
			this.nodes = new NodeScore[nodes.Length];
			for (int i = 0; i < nodes.Length; i++)
			{
				this.nodes[i] = new NodeScore()
				{
					node = Encoding.UTF8.GetBytes(nodes[i]),
					score = 0
				};
			}
		}

		public string GetNode(string key)
		{
			var keyBytes = Encoding.UTF8.GetBytes(key);

			uint maxScore = 0;
			byte[] maxNode = new byte[0];
			uint score = 0;

			for (int i = 0; i < this.nodes.Length; i++)
			{
				score = RdvHash(nodes[i].node, keyBytes);
				if (score > maxScore)
				{
					maxScore = score;
					maxNode = nodes[i].node;
				}
			}

			return Encoding.UTF8.GetString(maxNode);
		}

		public string[] GetN(int n, string key)
		{
			if (this.nodes.Length == 0 || n == 0)
			{
				return new string[0];
			}

			if (n > this.nodes.Length)
			{
				n = this.nodes.Length;
			}

			var keyBytes = Encoding.UTF8.GetBytes(key);
			for (int i = 0; i < this.nodes.Length; i++)
			{
				var ns = this.nodes[i];
				ns.score = RdvHash(ns.node, keyBytes);
			}

			Array.Sort<NodeScore>(this.nodes, (n1, n2) => n2.score.CompareTo(n1.score));

			var nodes = new string[n];
			for (int i = 0; i < n; i++)
			{
				nodes[i] = Encoding.UTF8.GetString(this.nodes[i].node);
			}
			return nodes;
		}

		private uint RdvHash(byte[] node, byte[] key)
		{
			var hashBytes = MergeBytes(key, node);
			var digest = HashAlgorithm.ComputeHash(hashBytes);
			var hash = BitConverter.ToUInt32(digest, 0);
			return hash;
		}

		private byte[] MergeBytes(byte[] front, byte[] back)
		{
			byte[] combined = new byte[front.Length + back.Length];
			Array.Copy(front, combined, front.Length);
			Array.Copy(back, 0, combined, front.Length, back.Length);
			return combined;
		}
	}
}