using System.Diagnostics;
using System.Runtime.InteropServices;

using SDL3;

class Program
{
	static void Main()
	{
		SDL.InitSubSystem(SDL.InitFlags.Audio);

		var desired =
			new SDL.AudioSpec()
			{
				Freq = 48000,
				Format = SDL.AudioFormat.AudioS16LE,
				Channels = 2,
			};

		s_generator = GenerateSamples().GetEnumerator();

		var stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, desired, Callback, default);

		SDL.PauseAudioDevice(SDL.GetAudioStreamDevice(stream));
		SDL.ResumeAudioDevice(SDL.GetAudioStreamDevice(stream));

		Console.WriteLine("Menu:");
		Console.WriteLine("- d: cause a breakpoint");
		Console.WriteLine("- r: reinitialize audio");
		Console.WriteLine("- s: completely reinitialize SDL");
		Console.WriteLine("- q: exit");

		while (true)
		{
			var cmd = Console.ReadLine();

			if ((cmd == null) || (cmd == "q"))
			{
				Console.WriteLine("Bye");
				break;
			}

			if (cmd == "d")
			{
				Console.WriteLine("Break! (If you don't have a debugger attached, this will do nothing)");
				Debugger.Break();
			}

			if (cmd == "r")
			{
				Console.WriteLine("Create audio device stream");

				SDL.PauseAudioDevice(SDL.GetAudioStreamDevice(stream));

				stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, desired, Callback, default);

				SDL.PauseAudioDevice(SDL.GetAudioStreamDevice(stream));
				SDL.ResumeAudioDevice(SDL.GetAudioStreamDevice(stream));
			}

			if (cmd == "s")
			{
				Console.WriteLine("Shutdown and restart SDL");

				SDL.PauseAudioDevice(SDL.GetAudioStreamDevice(stream));
				SDL.CloseAudioDevice(SDL.GetAudioStreamDevice(stream));

				SDL.Quit();

				SDL.InitSubSystem(SDL.InitFlags.Audio);

				stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, desired, Callback, default);

				SDL.PauseAudioDevice(SDL.GetAudioStreamDevice(stream));
				SDL.ResumeAudioDevice(SDL.GetAudioStreamDevice(stream));
			}
		}
	}

	static IEnumerator<(short Left, short Right)> s_generator = new List<(short, short)>().GetEnumerator();

	static byte[]? s_buffer;

	static void Callback(IntPtr userData, IntPtr stream, int additionalAmount, int totalAmount)
	{
		if ((s_buffer == null) || (s_buffer.Length < additionalAmount))
			s_buffer = new byte[additionalAmount * 2];

		// Assumes additionalAmount will always be a multiple of the sample frame size.

		var samples = MemoryMarshal.Cast<byte, short>(s_buffer.AsSpan().Slice(0, additionalAmount));

		for (int i=0; i < samples.Length; i += 2)
		{
			const short Zero = 0;

			var sample = s_generator.MoveNext() ? s_generator.Current : (Left: Zero, Right: Zero);

			samples[i] = sample.Left;
			samples[i + 1] = sample.Right;
		}

		SDL.PutAudioStreamData(stream, s_buffer, additionalAmount);
	}

	static IEnumerable<(short Left, short Right)> GenerateSamples()
	{
		double signalFrequency = 432;
		double panModulationFrequency = 1.5;
		double vibratoFrequency = 5;

		double samplesPerSignalCycle = 48000 / signalFrequency;
		double samplesPerModulationCycle = 48000 / panModulationFrequency;
		double samplesPerVibratoCycle = 48000 / vibratoFrequency;

		const double TwoPI = 2 * Math.PI;

		double advanceSignal = TwoPI / samplesPerSignalCycle;
		double advancePanModulation = TwoPI / samplesPerModulationCycle;
		double advanceVibrato = TwoPI / samplesPerVibratoCycle;

		double signalPhase = 0;
		double modulationPhase = 0;
		double vibratoPhase = 0;

		const double VibratoDepthFactor = 0.014;

		while (true)
		{
			double vibratoShift = Math.Sin(vibratoPhase);

			double vibratoAdjustment = Math.Pow(2.0, vibratoShift * VibratoDepthFactor);

			signalPhase += advanceSignal * vibratoAdjustment;
			if (signalPhase > TwoPI)
				signalPhase -= TwoPI;

			modulationPhase += advancePanModulation;
			if (modulationPhase > TwoPI)
				modulationPhase -= TwoPI;

			vibratoPhase += advanceVibrato;
			if (vibratoPhase > TwoPI)
				vibratoPhase -= TwoPI;

			double ampLeft = Math.Sin(signalPhase) * Math.Sin(modulationPhase);
			double ampRight = Math.Sin(signalPhase) * Math.Cos(modulationPhase);

			yield return ((short)(ampLeft * 25000), (short)(ampRight * 25000));
		}
	}
}
