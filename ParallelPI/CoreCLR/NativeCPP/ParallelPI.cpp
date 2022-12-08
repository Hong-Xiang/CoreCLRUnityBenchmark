#include <iostream>
#include <vector>
#include <algorithm>
#include <execution>

struct PiCalcuateChunk
{

public:
	PiCalcuateChunk(long samples, double relativeOffset) : Samples(samples), RelativeOffset(relativeOffset) {}
	long Samples;
	double RelativeOffset;
	double Calculate() const
	{
		double result = 0.0;
		double step = 1.0 / Samples;
		for (auto i = 0; i < Samples; i++)
		{
			auto x = (i + RelativeOffset) * step;
			auto f = 4.0 / (1.0 + x * x);
			result += f * step;
		}
		return result;
	}
};

int test_once(int chunkCount, long chunkSize) {
	auto chunks = std::vector<PiCalcuateChunk>();
	for (auto i = 0; i < chunkCount; i++)
	{
		chunks.emplace_back(chunkSize, (double)(i + 0.5) / chunkCount);
	}
	std::atomic<double> result = 0.0;
	auto pre = std::chrono::system_clock::now();
	std::for_each(std::execution::par_unseq, std::begin(chunks), std::end(chunks), [&](PiCalcuateChunk const& chunk) {
		result.fetch_add(chunk.Calculate());
		});
	auto past = std::chrono::system_clock::now();
	auto eta = std::chrono::duration_cast<std::chrono::microseconds>(past - pre);
	std::cout << "result: " << result / chunkCount << ", time: " << (double)eta.count() / 1000 << std::endl;
	return eta.count();
}


int main() {
	const int ChunkCount = 65536;
	const long ChunkSize = 4096;
	int repeat = 16;
	int total_mus = 0;
	for (auto i = 0; i < repeat; i++) {
		total_mus += test_once(ChunkCount, ChunkSize);
	}
	std::cout << "average ms: " << (double)total_mus / repeat / 1000 << std::endl;
	return 0;
}