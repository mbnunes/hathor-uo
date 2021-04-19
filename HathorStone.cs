using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Gumps;
using System.Net;
using System.IO;
using Server.Network;

namespace Server.Engines
{
	class HathorStore : Gump
	{
		
		private Mobile m_From;
		private HathorStone m_Stone;
		private static string serverwallet = "";
		private static string passphrase = "teste1234";
		private static string seedkey = "default";
		private string playerwallet;
		
		public static void Initialize() /* roda sempre que o server iniciar */
		{
			StartWallet();
		}
		
		public HathorStore(HathorStone stone, Mobile from) : base(30, 30)
		{
			m_Stone = stone;
			m_From = from;

			if (!m_Stone.m_HathorWallets.TryGetValue(from, out playerwallet))
				playerwallet = "";

			Dragable = true;
			Closable = true;
			Resizable = false;
			Disposable = false;
			AddPage(0);
			AddBackground(225, 151, 535, 279, 9200);
			AddLabel(404, 173, 0, @"Ultima Online's Hathor Exchenge");
			AddLabel(409, 214, 0, @"Your Hathor Wallet");
			AddLabel(439, 303, 0, @"Gold Amount");
			AddBackground(244, 243, 499, 54, 3500);
			AddBackground(444, 328, 87, 54, 3500);
			AddTextEntry(271, 254, 448, 31, 0, 0, playerwallet);
			AddTextEntry(468, 345, 37, 24, 0, 1, "1");
			AddButton(671, 388, 247, 248, 1, GumpButtonType.Reply, 0);
			AddButton(711, 169, 1604, 248, 0, GumpButtonType.Reply, 0);
			AddLabel(537, 346, 0, @"x 1000");
		}

		public override void OnResponse(NetState sender, RelayInfo info)
		{

			if (info.ButtonID == 1)
			{
				string wallet = info.GetTextEntry(0).Text;
				string sgold = info.GetTextEntry(1).Text;

				string message = null;
				if (!int.TryParse(sgold, out int gold) && gold < 1)
				{
					message = $"Invalid gold value, please try again";
				}
				else if (String.IsNullOrEmpty(wallet) || String.IsNullOrWhiteSpace(wallet))
				{
					message = "Invalid Wallet ID";
				}
				else if (!m_From.Backpack.ConsumeTotal(typeof(Gold), gold*1000))
				{
					message = $"You dont have {gold*1000:#,##0} gold coins in your backpack!";
				}


				if (message != null)
				{
					m_From.SendGump(new NoticeGump(1060637, 30720, message, 0xFFC000, 420, 280, null, null));
					return;
				}

				if (m_Stone.m_HathorWallets.ContainsKey(m_From))
					m_Stone.m_HathorWallets[m_From] = wallet;
				else
					m_Stone.m_HathorWallets.Add(m_From, wallet);

				m_From.LocalOverheadMessage(MessageType.Regular, 0x3b2, true, "Processing your payment...");
				int units = gold;
				ProcessPayment(m_From, wallet, units).ConfigureAwait(false);
			}
		}

		public Task ProcessPayment(Mobile from, string playerWallet, int coins)
		{
			return Task.Run(() =>
			{				
				SendTxToPlayer(from, playerWallet, coins);
			});
		}

		public void SendTxToPlayer(Mobile player, string walletPlayer, int units)
		{
			try
			{
				var request = (HttpWebRequest)WebRequest.Create("http://localhost:8000/wallet/simple-send-tx");
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.Headers.Add("X-Wallet-Id", serverwallet);


				var postData = string.Join("&", $"address={walletPlayer}", $"value={units}");

				var data = Encoding.ASCII.GetBytes(postData.ToString());
				request.ContentLength = data.Length;

				using (var stream = request.GetRequestStream())
				{
					stream.Write(data, 0, data.Length);
				}

				var response = (HttpWebResponse)request.GetResponse();

				var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
				m_From.SendGump(new NoticeGump(1060637, 30720, responseString, 0x00A0EE, 420, 280, null, null));
				//string message = $"Congratulations!<br>You have converted {units*1000:#,##0} Gold Coins into {((float)units)/100:#,##0.00} HathorCoin! You can now check your Hathor wallet!";
				//m_From.SendGump(new NoticeGump(1060637, 30720, message, 0x00A0EE, 420, 280, null, null));
			}
			catch
			{
				string err = $"Cannot process your request. {units*1000:#,##0} gold coins has been returned to your backpack";
				m_From.SendGump(new NoticeGump(1060637, 30720, err, 0xFFC000, 420, 280, null, null));
				m_From.AddToBackpack(new Gold(units * 1000));
				return;
			}
		}
		
		public static void StartWallet()
		{
			try
			{
				var request = (HttpWebRequest)WebRequest.Create("http://localhost:8000/start");
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.Headers.Add("X-Wallet-Id", serverwallet);


				var postData = string.Join("&", $"wallet-id={serverwallet}", $"passphrase={passphrase}", $"seedKey={seedkey}");

				var data = Encoding.ASCII.GetBytes(postData.ToString());
				request.ContentLength = data.Length;

				using (var stream = request.GetRequestStream())
				{
					stream.Write(data, 0, data.Length);
				}

				var response = (HttpWebResponse)request.GetResponse();

				var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
				
				Console.WriteLine("Carteira Hathor iniciada");
			}
			catch
			{
				Console.WriteLine("Erro ao iniciar carteira Hathor");
			}
		}
		
	}
	class HathorStone : Item
	{
		public Dictionary<Mobile, string> m_HathorWallets;
		[Constructable]
		public HathorStone() : base(0x1851)
		{
			m_HathorWallets = new Dictionary<Mobile, string>();
		}

		public HathorStone(Serial serial) : base(serial)
		{
			m_HathorWallets = new Dictionary<Mobile, string>();
		}

		public override void OnDoubleClick(Mobile from)
		{
			from.CloseGump(typeof(HathorStore));
			from.SendGump(new HathorStore(this, from));

		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write((int)1);

			writer.Write(m_HathorWallets.Count);
			foreach (var wallet in m_HathorWallets)
			{
				writer.Write(wallet.Key);
				writer.Write(wallet.Value);
			}
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			int version = reader.ReadInt();

			int count = reader.ReadInt();

			for (int i = 0; i < count; i++)
			{
				m_HathorWallets.Add(reader.ReadMobile(), reader.ReadString());
			}
		}
	}
}
